![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Health Concept

A container that runs is not yet an installation that works. The process can be up, the port open and every anonymous page answering while nobody can sign in because the signing key was never mounted, or while the database the pages read went away with a volume. An orchestrator that only asks whether the port accepts connections routes users to exactly such an instance, and a restart that would have fixed it never happens.

**KleeneStar** therefore answers the question an orchestrator asks - *can this instance serve its users right now?* - through the health model WebExpress provides. The framework already knows whether the application could be created at all, which is where the one-time initialization (migration and seed) fails; KleeneStar contributes the checks only it can make:

- The configured database answers, exists and carries the schema this build expects.
- The sign-in settings can issue tokens.

## The Probe

The endpoint is WebExpress's, not KleeneStar's. `GET /health` (and `HEAD`) is answered on every listener, before authentication and routing, independent of the application's context path (`/kleenestar`) and of `ExternalUri`. It sets no cookie and redirects nowhere.

|Answer                           |Meaning
|---------------------------------|-----------------------------------------------------------------
|`200 {"status":"healthy"}`       |The host has started, is not stopping, and every health component of every installed plugin passed.
|`503 {"status":"unhealthy",…}`   |At least one did not - or the host is still starting, or already draining.
|`405`                            |Any method other than `GET` and `HEAD`.

The response never says *which* check failed or why. That is deliberate: the endpoint is unauthenticated and a connection string, a file path or an exception message is not for anybody who can reach the port. The reason is written to the **server log** - application id, component id and the diagnostic the check returned - which is where an operator looking at a `503` goes next.

The framework's own conditions come first: the HTTP server has to be running, every component manager has to exist, and every declared application has to have been created. A host without any health component would therefore be healthy as soon as it listens - which is why the checks below matter.

`GET /health/live` answers the framework's conditions alone and runs none of the checks below. It is the endpoint for a liveness probe that must not depend on a database the pod does not own (see [Kubernetes](#kubernetes)).

## The Checks

A health component is a `public sealed` class implementing `WebExpress.WebCore.WebHealth.IHealth`. WebExpress's `HealthManager` finds it by scanning the plugin's assembly and binds it once per application the plugin is associated with; nothing registers it. The core's components live in `WebHealth/` and are bound to `KleeneStarApplication`:

|Component              |Budget |Unhealthy when
|-----------------------|-------|------------------------------------------------------------------
|`DatabaseHealth`       |3 s    |The configured provider cannot be reached, the database does not exist, or migrations of this build are missing.
|`AuthenticationHealth` |1 s    |The framework cannot issue a token: `WebExpress:Authentication` is missing or unusable, or no token store is available.

The checks run concurrently, and a check that does not answer within its budget (`[HealthTimeout]`) counts as failed. An exception thrown from a check counts as failed as well, and the framework logs it with its stack trace - so a check does not catch what it cannot explain better than the exception does.

### Initialization

Migration and seed run once, in the constructor of `KleeneStarApplication`, and a failure is left to escape it. WebExpress catches it, logs it, and carries on without the application - but records it in `IApplicationManager.FailedApplications`, and the framework conditions of **both** `/health` and `/health/live` fail for as long as it is there. There is no check of KleeneStar's own for it: the health components are bound to the application, so a check that was supposed to report the failed application would have vanished with it.

Nothing retries the initialization within the process - a check must not repair what it checks - so the remedy is a restart, which is what a liveness probe on `/health/live` does. Against an unreachable database server that is a restart loop until the server answers, which is the intended behaviour: the pod never receives traffic in between.

(Until WebExpress recorded failed applications, the swallowed exception took the application and its health bindings with it and `/health` answered `200` for a host serving nothing but 404s; KleeneStar worked around it with a startup check of its own, removed again on 2026-09-30.)

### Database

`DatabaseHealth` asks the provider the installation is configured with (`Plugins:kleenestar.core:Database`, see `ModelHub.CreateDbContext`) and writes no SQL of its own:

1. **Exists** - asked first, through the provider's database creator. A sqlite connection creates a missing file, and a probe must not leave an empty database behind where a volume went missing.
2. **Round trip and schema** - the migration history is read and compared with the migrations of the provider assembly. A migration this build knows and the database lacks is a failure; a migration the database has and this build does not know is **not** - that is a newer instance that already migrated during a rolling update.

A provider without schema support (the in-memory provider of the tests) is judged by whether it accepts a connection.

### Sign-in Settings

Without a usable `WebExpress:Authentication` section every sign-in fails, while every anonymous page still answers - the state a container ends up in when its signing key secret was never mounted. `AuthenticationHealth` asks the framework (`IIdentityManager.IsAuthenticationConfigured`) for the application it is bound to, so it follows every rule a sign-in enforces - issuer and audience set, a Base64 signing key of at least 256 bits, valid lifetimes - without restating them. A token store registered by a plugin counts as well as the file store under `TokenStorePath`. When a present section is unusable, the framework writes the reason to the server log, never the key.

A missing token store directory is created by that question, as the first sign-in would; a directory that cannot be created fails the check with the exception.

### What Is Deliberately Not Checked

- **External identity providers** (OpenID Connect authorities). Their outage affects the accounts of that source, not the installation; failing readiness for it would take every instance out of service while local accounts could still sign in.
- **The portal.** It has no dependency of its own - it reads the core's managers - so the core's checks already cover it.
- **Writability of the data volume.** Proving it means writing, and a probe that writes every few seconds is a load of its own. A read-only volume shows up in the server log on the first write.

## Docker

`KleeneStar/Dockerfile` declares the probe:

```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --start-period=120s --start-interval=5s --retries=3 \
    CMD ["timeout", "4", "bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /health HTTP/1.1\\r\\nHost: 127.0.0.1\\r\\nConnection: close\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q '^HTTP/1\\.[01] 200 '"]
```

- The `aspnet` image carries neither `curl` nor `wget`, and installing one only for the probe widens the image for nothing. `bash` and `/dev/tcp` are enough for a plain HTTP request. An image based on a variant without `bash` (Alpine, chiseled) needs another probe.
- The longest budget of a check is 3 s, so the request gets 4 s and Docker 5 s. A check with a longer budget has to raise both.
- The start period covers the first start, which migrates and seeds; probes run every five seconds during it, and a success ends it early.
- The port is the one the Dockerfile's endpoint listens on (`8080`). An image that overrides `WEBEXPRESS_WebExpress__Endpoints__0__Uri` to another port overrides the healthcheck too (`healthcheck:` in Compose).

`docker compose ps` shows the state, `docker inspect --format '{{json .State.Health}}' <container>` the last probes. Standalone Docker only **records** health: `restart: unless-stopped` restarts a container whose process ended, not one that is unhealthy. Acting on the state is the orchestrator's job.

## Kubernetes

The container needs no probe binary - the kubelet calls the endpoint itself:

```yaml
terminationGracePeriodSeconds: 45   # above ShutdownTimeoutSeconds (30), see Shutdown below
containers:
  - name: kleenestar
    image: kleenestarorg/kleenestar:<version>
    ports:
      - name: http
        containerPort: 8080
    env:
      - name: WEBEXPRESS_WebExpress__Authentication__SigningKey
        valueFrom:
          secretKeyRef:
            name: kleenestar
            key: signing-key
      - name: WEBEXPRESS_WebExpress__ExternalUri
        value: https://kleenestar.example.org/
    startupProbe:
      httpGet:
        path: /health
        port: http
      periodSeconds: 5
      timeoutSeconds: 5
      failureThreshold: 60        # five minutes for the first migration and seed
    readinessProbe:
      httpGet:
        path: /health
        port: http
      periodSeconds: 10
      timeoutSeconds: 5
      failureThreshold: 1
    livenessProbe:
      httpGet:
        path: /health/live
        port: http
      periodSeconds: 30
      timeoutSeconds: 5
      failureThreshold: 3
```

**Readiness** is what `/health` is for: an instance that fails it is taken out of the Service until it passes again, which is exactly right for a database that is briefly unreachable.

**Liveness** belongs on `/health/live`, which judges the framework alone: a host stuck in startup or shutdown, a component manager or an application that could not be created. Those are the faults a restart can cure - a failed migration among them. It runs none of the checks above, so an outage of a database server does not restart every pod in turn for nothing; readiness keeps them out of the Service instead, and the log says why. Liveness on `/health` would do exactly that.

Two further consequences of the defaults:

- **sqlite means one replica.** The database is a file on a `ReadWriteOnce` volume; two pods writing it corrupt it. Scale-out needs a database server.
- **Shutdown.** WebExpress answers `503` from the moment it starts stopping, so readiness drops before the listener closes. The image sets `WebExpress:Shutdown` to `graceful` (`WEBEXPRESS_WebExpress__Shutdown`), which lets admitted requests finish within `ShutdownTimeoutSeconds` (30 s by default); `terminationGracePeriodSeconds` - Kubernetes' default is exactly 30 - and Compose's `stop_grace_period` (45 s in `docker-compose.yml`) have to be longer, or the process is killed while it drains.

## Adding a Check

A plugin that brings a dependency the installation cannot serve without brings its check with it:

- `public sealed`, implementing `IHealth`, in the plugin's own assembly - not sealed means not discovered, silently. `UnitTestHealthDiscovery` enforces this for the core.
- A private constructor; the framework injects `IHealthContext`, `IApplicationContext`, `IHttpServerContext`, `IComponentHub` and the component managers.
- `[HealthTimeout(ms)]` with a budget that fits the probe timeouts above (4 s at most, or raise them).
- Read-only and small. A check must not repair, migrate, write business data or call `/health`.
- Unhealthy while the dependency is still initializing, and a diagnostic for the log that explains the failure without a secret in it.
- Only what is **critical**. A dependency whose outage degrades a feature but leaves the installation usable does not belong in the aggregate - it would take every instance out of service for it.
