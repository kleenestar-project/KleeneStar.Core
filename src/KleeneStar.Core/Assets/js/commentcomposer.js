var kleenestar = kleenestar || {}
kleenestar.core = kleenestar.core || {}

/**
 * Opens a comment composer on the WYSIWYG form instead of on its one-line trigger.
 *
 * The framework's ControlDataCommentComposer always mounts collapsed: it paints a single-line
 * button ("Write a comment…") and only builds the category picker, the rich-text editor and the
 * send button once that button is clicked or focused. There is no server-side option and no data
 * attribute to start expanded, so a page that wants the form to be there cannot say so - see the
 * remedy note in CommentComposerExpandScript.
 *
 * A composer opts in by carrying the ks-comment-composer-open class, which the fragment authoring
 * it declares. This expands exactly those, once each:
 *
 *     <div class="wx-webapp-comment-composer ks-comment-composer-open" …>
 *
 * The expansion goes through the composer's own trigger rather than through its internals, so it
 * uses the same path a user's click takes and keeps working when the control's private fields are
 * renamed. What has to be undone afterwards is the focus: expanding focuses the editor, which on
 * page load would scroll straight past the issue to the form at the bottom of it. The focus is
 * therefore taken back and the scroll position restored, before the browser paints - so the reader
 * arrives at the top of the issue, with the form already open below it.
 *
 * "Once each" is deliberate: a reader who closes the form with Cancel has said they do not want
 * it, and re-opening it under them would be the opposite of a convenience.
 *
 * The composer element belongs to the framework's controller, so this is no control of its own
 * but a singleton watching the document.
 */
kleenestar.core.CommentComposerOpener = new class {
    /**
     * The class a composer carries to ask for the form to be shown right away.
     */
    OPT_IN_CLASS = "ks-comment-composer-open";

    /**
     * The trigger the framework's composer paints while it is collapsed. Its presence is also
     * what tells us the controller has mounted and built its DOM.
     */
    TRIGGER_SELECTOR = ".wx-comment-composer-trigger";

    /**
     * The class the composer carries while it is collapsed.
     */
    COLLAPSED_CLASS = "wx-comment-composer-collapsed";

    /**
     * The composers that have been opened already, so a Cancel is not undone on the next
     * mutation.
     */
    _opened = new WeakSet();

    /**
     * Constructor
     */
    constructor() {
        // the composer's controller is created by the framework's registry, which runs on
        // DOMContentLoaded for the initial markup and on insertion for anything added later.
        // Watching the document covers both without having to know which of the two applies
        // here, and the WeakSet keeps the repeated sweeps free of effect once every composer is open.
        new MutationObserver(() => this.sweep()).observe(document.documentElement, { childList: true, subtree: true });

        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", () => this.sweep());
        } else {
            this.sweep();
        }
    }

    /**
     * Opens one composer on its form, leaving focus and scroll position where they were.
     * @param {HTMLElement} host - The composer host element.
     */
    open(host) {
        const trigger = host.querySelector(this.TRIGGER_SELECTOR);

        // no trigger means the controller has not built its DOM yet; the observer calls again
        // once it has
        if (!trigger || !host.classList.contains(this.COLLAPSED_CLASS)) {
            return;
        }

        this._opened.add(host);

        const x = window.scrollX;
        const y = window.scrollY;
        const active = document.activeElement;

        trigger.click();

        // the composer focuses its editor from a microtask, so the correction has to run after
        // one - and before the paint, or the reader sees the page jump and come back
        requestAnimationFrame(() => {
            if (document.activeElement !== active && host.contains(document.activeElement)) {
                document.activeElement.blur();
            }

            if (window.scrollX !== x || window.scrollY !== y) {
                window.scrollTo(x, y);
            }
        });
    }

    /**
     * Opens every composer that asked for it and has not been opened yet.
     */
    sweep() {
        for (const host of document.getElementsByClassName(this.OPT_IN_CLASS)) {
            if (!this._opened.has(host)) {
                this.open(host);
            }
        }
    }
};
