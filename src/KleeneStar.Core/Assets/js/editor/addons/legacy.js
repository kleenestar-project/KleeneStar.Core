/**
 * Registers the legacy names ks-toc and ks-status: stored documents keep the names the table of
 * contents and the status label had while they were KleeneStar's, so they delegate to the
 * framework's add-ons of the same purpose (toc, status), registered before this file.
 */
for (const name of ["toc", "status"]) {
    const definition = webexpress.webui.EditorAddOns.get(name);
    if (definition) {
        webexpress.webui.EditorAddOns.register("ks-" + name, { ...definition });
    }
}
