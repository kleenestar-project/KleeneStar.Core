# Authentication: internal and external accounts

An account (`Identity`) is either **internal** - this installation keeps its password - or
**external** - a source outside the installation owns its credentials. Which one it is, is the
account's `AuthenticationSource`: `null` for internal, otherwise the key of a registered source.

## Sources

There is no enum of sources. `AuthenticationSourceCatalog` is an open registry, like the object
kinds and renderers; the core registers the internal source (`local`), a plugin registers its own
`IAuthenticationSource`:

| Member | Meaning |
|---|---|
| `Key` | What the accounts store in `AuthenticationSource`. `local` is stored as `null`. |
| `IsExternal` | The credentials are owned elsewhere: no password is set, changed or reset here. |
| `AcceptsPassword` | The sign-in form's password is checked by this source (a directory: yes; an identity provider the browser is sent to: no). |
| `Provisions` | A sign-in that stands for no stored account creates one. |
| `VerifyPassword` | Checks an offered password; asked only when `AcceptsPassword`. |

**An account whose key names no registered source signs in by no means.** The renderer catalog
falls back to a default for an unknown key; this one must not, because the only fallback would be
the internal password check of an account whose password was never kept here.

## Signing in with a password

`/api/1/session` asks `ICredentialManager.Authenticate(login, password)`: the account is found by
user name or e-mail (active accounts only), and its source verifies the password. A refused
attempt costs a hash verification whatever the reason, so the answer time does not tell an
unknown name from a known one. The framework's per-name lockout and the audit's `SignInFailed`
apply unchanged.

The hash is ASP.NET's `PasswordHasher` (`KleeneStar.Model/IdentityPassword`), the format
WebExpress's own `LocalIdentityProvider` verifies. A hash that asks to be refreshed is rewritten on
the next successful sign-in.

## Setting a password

- **The owner** changes it on the profile's security page, proving the current one
  (`/api/1/profile/password`). An external account sees where its password is managed instead.
- **An administrator** cannot see or choose a password. With no mail delivery, they issue a
  **one-time link** (identity table -> *Create password link*, `/api/1/identities/passwordresets`).
  It is shown once, valid for 24 hours, spent by its first use, and issuing a new one spends the
  open one. Only the SHA-256 of its 256-bit secret is stored (`PasswordReset`).
- **The link** opens `/kleenestar/setpassword?token=...`, which sets the password without a
  sign-in (`/api/1/password/reset`): the secret is the authorization.

Rules (`ICredentialManager.ValidatePassword`): 8 to 256 characters, not the account's user name,
e-mail address or display name.

The credential columns (`PasswordHash`, `PasswordChanged`, `ExternalSubject`) are written by the
dedicated `ModelHub` methods alone; an ordinary identity update leaves them untouched. Moving an
account to another source drops its hash and its subject.

## External sign-in

`ICredentialManager.SignInExternal(source, claims)` answers the stored account an external sign-in
stands for:

1. the account with that source and `ExternalSubject`;
2. otherwise the one account an administrator created for that source that has no subject yet,
   when the source vouches for its e-mail address (`email_verified`) - the subject is then bound;
3. otherwise a new account, when the source provisions;
4. otherwise nobody.

An internal account is never matched by name or e-mail - that would hand an account of this
installation to whoever controls a directory entry of the same name. For the same reason the
session resolves a token by its id only.

OpenID Connect sources are configured (see the settings readme) and registered at start by
`OpenIdConnectAuthenticationSource`, which rides WebExpress's authorization-code flow and answers
the stored account from `MapIdentity`, so the issued token carries the stored id.

## Who administers accounts

The permission model's chains start at a workspace; an account belongs to none. Until there is an
installation scope, the account administrators are the active members of the administrators
group (`Group.AdministratorsId`, the seeded `Admin`), checked by `AccountAuthorization`. It gates
reset links and every write of `/api/1/identities`, and fails closed.

## Audit

`PasswordChanged` (actor: the owner, or nobody for a link) and `PasswordResetIssued` (actor: the
administrator) are recorded; neither a password nor a secret reaches the log.

## Known gaps

- The WebExpress login page has no slot for *sign in with ...* buttons, and the OIDC callback
  answers JSON instead of redirecting into the application; the OIDC path works but is reachable
  only by hand until the framework offers both.
- No LDAP source ships; it would be a plugin registering a source with `AcceptsPassword = true`.
- A changed password does not revoke credentials already issued; they expire with their lifetime.
- No two-factor authentication, no self-service "forgot password" (there is no mail delivery).
