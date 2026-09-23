/**
 * Keeps a signed-in page signed in.
 *
 * A sign-in is a short-lived access token plus a refresh token, both in cookies the page cannot
 * read. The access token simply expires; nothing in WebExpress renews it, so without this the
 * user turned anonymous mid-sentence and the next save was refused without a word. The page
 * learns when the access token ends from <meta name="kleenestar.session"> (unix seconds, only
 * rendered for a signed-in sign-in) and asks /api/auth/refresh for a new pair shortly before.
 *
 * Refresh tokens rotate, and presenting one twice revokes the whole sign-in as a replay. Every
 * open tab would try, so the tabs agree: the refresh runs under a Web Lock, and the new expiry
 * is shared through localStorage - a tab that finds it already moved forward does nothing.
 */
(function () {
    "use strict";

    const meta = document.querySelector('meta[name="kleenestar.session"]');

    if (!meta) {
        return;
    }

    const margin = 120;
    const storageKey = "kleenestar.session.expires";
    const lockName = "kleenestar.session.refresh";
    const refreshMeta = document.querySelector('meta[name="kleenestar.session.refresh"]');
    const refreshUri = (refreshMeta && refreshMeta.getAttribute("content")) || "/api/auth/refresh";

    let expires = Number(meta.getAttribute("content")) || 0;
    let timer = null;
    let stopped = false;

    const now = () => Date.now() / 1000;

    const shared = () => {
        try {
            return Number(window.localStorage.getItem(storageKey)) || 0;
        } catch (e) {
            return 0;
        }
    };

    // the latest expiry any tab has seen - the cookie is shared, so is its deadline
    const known = () => Math.max(expires, shared());

    const remember = (value) => {
        expires = Math.max(expires, value);
        try {
            if (value > shared()) {
                window.localStorage.setItem(storageKey, String(value));
            }
        } catch (e) {
            // storage may be unavailable (private mode); the tab then keeps its own deadline
        }
    };

    const schedule = () => {
        if (stopped) {
            return;
        }
        window.clearTimeout(timer);
        const wait = Math.max(5, known() - margin - now());
        // setTimeout cannot wait longer than ~24 days
        timer = window.setTimeout(run, Math.min(wait, 2000000) * 1000);
    };

    const refresh = async () => {
        // another tab refreshed in the meantime: the cookies are already new
        if (known() - now() > margin) {
            return true;
        }
        try {
            const response = await fetch(refreshUri, {
                method: "POST",
                credentials: "same-origin",
                headers: { "X-WebExpress-Auth": "1" }
            });
            if (!response.ok) {
                // the sign-in ended - signed out, revoked, or past its absolute lifetime
                return false;
            }
            const data = await response.json().catch(() => null);
            const at = data && data.expiresAt ? Date.parse(data.expiresAt) / 1000 : 0;
            remember(at || now() + 5 * 60);
            return true;
        } catch (e) {
            // offline or the server restarting: try again a little later
            window.setTimeout(run, 30000);
            return null;
        }
    };

    const run = async () => {
        let result;
        if (navigator.locks && navigator.locks.request) {
            result = await navigator.locks.request(lockName, refresh);
        } else {
            result = await refresh();
        }
        if (result === false) {
            stopped = true;
            return;
        }
        if (result === true) {
            schedule();
        }
    };

    // a tab that slept through its deadline catches up as soon as it is looked at again
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState !== "visible") {
            return;
        }
        if (known() - now() <= margin) {
            run();
        } else {
            schedule();
        }
    });

    window.addEventListener("storage", (event) => {
        if (event.key === storageKey) {
            schedule();
        }
    });

    remember(expires);
    schedule();
})();
