![KleeneStar](https://raw.githubusercontent.com/kleenestar-project/.github/main/docs/assets/img/banner.png)

# KleeneStar Renderer Concept

An object type answers one question — *where do the objects of this class appear?* Documents form a page tree, blog posts a timeline, issues a filterable work-item list, assets an inventory. It says nothing about the second question, which is just as consequential for the person in front of the screen: *what happens when I open one of them?*

For a long time the two answers were fused. A document was a page tree entry **and** a WYSIWYG surface; an issue was a work-item list entry **and** a field mask. Nothing in the model said so — it was simply which fragments happened to be scoped to which routes. The consequence was that a structured record and a written page could not stand side by side in the same tree, because being in the tree already meant being prose.

A **renderer** is the second answer, made explicit and made a choice. `Class.Renderer` names the surface the objects of the class are read and written through, independently of `Class.Kind`. A class of kind *document* with the **form** renderer stands in the same page tree, in the same order, under the same parent, and opens as an input mask; a class of kind *document* with the **prose** renderer opens in the WYSIWYG editor, as it always has. Kind and renderer are orthogonal, and neither knows about the other.

Renderers bundle:
- A key persisted per class, chosen from an open, plugin-extensible catalog — there is no enum.
- A default per object kind, so a class that names none behaves the way its kind always has.
- Two surfaces per renderer, the reading one and the writing one, contributed as ordinary fragments.
- A condition that decides between them, so several renderers may be scoped to one route and exactly one draws.

## The Model: One Nullable Column

```
╔══════════════════════════════════════════════════════════════════════════════════════╗
║                          KleeneStar Kind / Renderer Matrix                           ║
╠══════════════════════════════════════════════════════════════════════════════════════╣
║                                                                                      ║
║                   Class.Kind  ──────────────►  where the objects appear              ║
║                   Class.Renderer ───────────►  how one of them opens                 ║
║                                                                                      ║
║                  ┌────────────┬───────────────────┬───────────────────┐              ║
║                  │            │   prose           │   form            │              ║
║                  ├────────────┼───────────────────┼───────────────────┤              ║
║                  │ document   │ page tree,        │ page tree,        │              ║
║                  │            │ WYSIWYG   (def.)  │ input mask        │              ║
║                  │ blog       │ timeline,         │ timeline,         │              ║
║                  │            │ WYSIWYG   (def.)  │ input mask        │              ║
║                  │ issue      │ —                 │ work items,       │              ║
║                  │            │                   │ input mask (def.) │              ║
║                  │ asset      │ —                 │ inventory,        │              ║
║                  │            │                   │ input mask (def.) │              ║
║                  └────────────┴───────────────────┴───────────────────┘              ║
║                                                                                      ║
╚══════════════════════════════════════════════════════════════════════════════════════╝
```

`Class.Renderer` is a nullable string of at most 64 characters, and `ObjectRenderer` holds the two core keys (`prose`, `form`) exactly the way `ObjectKind` holds the kind keys. It is a free string and not an enum for the same reason: a plugin must be able to contribute one without a migration, and a key must survive the plugin being uninstalled.

There are two deliberate asymmetries with the kind beside it.

**Unset is a state, not a default.** `ObjectRenderer.Normalize` maps blank to `null`, where `ObjectKind.Normalize` maps blank to *issue*. There is no renderer that is right for every kind — prose is right for a document and wrong for an issue — so "not set" means *follow the kind*, and the kind descriptor is asked what that is (`IObjectKind.DefaultRenderer`). A class that names nothing therefore keeps behaving the way its kind always has, and keeps following along when its kind is changed later.

**The renderer is not stamped onto the objects.** `Class.Kind` is copied onto every `Object.Kind` on create and update, because the overviews query by it. The renderer is read live from the class on every request, because it is presentation rather than data: switching a class from prose to form changes how its objects open on the next page load, and nothing has to be rewritten.

## The Catalog

`ObjectRendererCatalog` is the semantic lookup behind the persisted key, and the sibling of `ObjectKindCatalog` in every respect: a static registry, seeded with the core's descriptors, extended by `Register`, and deliberately not a gate on persistence — an unknown key survives in the data layer.

An `IObjectRenderer` carries a key, a label, a description, an icon, an order, and the kinds it serves. **An empty `Kinds` collection means every kind**, including kinds registered later. That is how the form renderer is declared: a mask needs nothing of a kind beyond its classes having fields and forms, and every class has both, so it is offered everywhere and is what an add-on kind gets for free. Prose declares *document* and *blog*, the two kinds that have a body to write.

Resolution is one method, `ResolveKey`, and it has three answers in order:

| The class…                                   | resolves to
|----------------------------------------------|-----------------------------------------
| names a registered renderer                  | that renderer
| names none                                   | `IObjectKind.DefaultRenderer` of its kind
| names one whose plugin is gone               | `IObjectKind.DefaultRenderer` of its kind

The third row is why the catalog does not gate persistence. A class whose renderer was contributed by an uninstalled plugin keeps the key in the database and reads as its kind meanwhile; reinstalling the plugin restores it without anybody having reconfigured anything.

## Fragments Decide, Not Pages

No page knows about renderers. `WWW/Document/{objectkey}/Index` sets a title and a breadcrumb, and did so before renderers existed; the same is true of the edit route beside it. What changed is that **two** fragments are now scoped to each of those routes, and a condition decides between them:

| Route                        | prose renderer                     | form renderer
|------------------------------|------------------------------------|-------------------------------
| `/document/{key}`            | `ObjectProseReadFragment`          | `ObjectFormReadFragment`
| `/document/{key}` (headline) | `ObjectProseEditButtonFragment`    | `ObjectFormEditButtonFragment`
| `/document/{key}/edit`       | `ObjectProseEditorPageFragment`    | `ObjectFormEditFragment`

`ObjectRendererCondition` is the whole mechanism: it resolves the object the request addresses, asks the catalog which renderer its class renders through, and answers whether that is the one this fragment draws. `ProseRendererCondition` and `FormRendererCondition` name one renderer each and carry no logic — a condition is bound through `[Condition<T>]`, which takes a type rather than a value.

**This is what makes the concept extensible rather than merely configurable.** A third renderer is a descriptor, a condition, and a pair of fragments. Nothing already written is edited, because nothing already written asks what else exists — each fragment only knows whether it is the one.

## The Two Core Renderers

**Prose** is what a document and a post have always been, and it is unchanged: the WYSIWYG surface writing into an [unpublished draft](kleenestar.draft.md), the reading view showing the last published text, the publish button ending both. See that document; nothing about it moved.

**Form** is the new one, and it is not new code so much as the issue detail standing somewhere else:

- *Reading* — `ObjectFormReadFragment` renders the class's `FormType.View` form as a **filled-in sheet**: a boxed page, a captioned band per tab, and one numbered, ruled line per field with the answer printed into it. Tags close it off, the way they close off the prose reading view, so switching a class between the renderers does not lose them.
- *Writing* — `ObjectFormEditFragment` renders the class's `FormType.Edit` form over `/api/1/objects`. It is the same mask as the issue edit dialog, built by the same `ObjectStructuredEditFormFragmentBase` from the same form of the same class; the two subclasses differ only in where they stand and which condition, if any, gates them.

Three details of the sheet are decisions rather than defaults.

**It is a form, not a property list.** A column of "name: value" rows reads as a record *about* the object; the reader should recognize the document they filled in. Hence the numbered lines — a printed form refers to its own lines by number — the section bands, and the ruled box that is there whether or not anybody wrote in it. The look lives in `kleenestar.css` under *form reading view*; the fragment only names the classes.

**The tabs become sections on one sheet.** A printed form is continuous, and hiding half its lines behind a second navigation reads worse than a page the eye can run down. The *editing* side keeps the tabs — there, one part at a time is what makes a long form fillable.

**An empty field is kept and drawn as an empty (hatched) box**, which is the opposite of what the reduced pane view does. The reason is what the two are for: a pane summarizes an object, while this *is* the object — an unanswered line is information, and dropping it would silently shorten the record.

One trap the sheet has to know about: a field whose name aliases a system attribute of the object — `Description`, `Summary` — has **no value row at all**. The mask names its inputs after the fields, and `/api/1/objects` binds a payload key matching a property of `Object` to the object itself (`UpsertFieldValues` skips it deliberately). Reading such a line from the value rows would show an empty box beside an edit form that has the text in it, so `ResolveAnswer` takes it from the object — and treats the description as rich text whatever type the field aliasing it declares.

There is no draft on the form side, and that is deliberate rather than missing. A draft is prose's answer to writing a long text over several sittings; a field is finished when it is filled in.

## Administration

The renderer is picked on the class dialogs, beside the object type, from the catalog — so a plugin that ships a renderer becomes selectable without those dialogs knowing it exists.

The list leads with **Follow the object type**, the entry that clears the field. It exists because the way back to the default has to be *offered*, not merely reachable by never having chosen: a single-select has no deselect, so without it a class could be given a renderer and never given one back. Its honest value would be the empty string, but an option carries its value in its element id and the selection control drops an empty id on the client — so the entry travels as the reserved token `ObjectRendererCatalog.Automatic` (`auto`) and `Unwrap` turns it back into the unset state at the endpoint, on all three write paths, before anything persists it. `Register` refuses `auto` as a renderer key, so no plugin can shadow the entry that clears the field.

The picker is **not** filtered by the chosen object type, because the type is picked in the same form and the dialogs are cached fragments that cannot safely rebuild their options per request. The gate is on the server instead: `/api/1/classes` overrides `Validate` and refuses a renderer the resulting kind does not offer, naming the ones it does. An unset renderer — and therefore the automatic entry — is always accepted. A pairing no fragment is gated on would leave the objects of the class with no reading view at all, which is why this is refused rather than merely discouraged.

`ClassManager.Add` and `Update` normalize the key on the way in, exactly as they normalize the kind — to `null` rather than to a default, per the asymmetry above.

## Seeded Example

The seed carries one document class of each renderer, so both surfaces are reachable on a fresh installation without anybody having configured anything: **Documentation** and **Knowledge** are prose, **Specification** (Software Development workspace) names the form renderer. All three stand in the page tree of their workspace; opening a Specification shows the mask, opening a Documentation shows the article.

## Related

- [Classes](kleenestar.class.md) — the class carries both the kind and the renderer.
- [Forms](kleenestar.form.md) — what the form renderer reads and writes through.
- [Drafts and publication](kleenestar.draft.md) — what the prose renderer reads and writes through.
- [Objects](kleenestar.object.md) — the kind, which the renderer is orthogonal to.
