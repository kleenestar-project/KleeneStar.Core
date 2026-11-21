using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebIndex.Queries;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

using ClassEntity = KleeneStar.Model.Entities.Class;
using TemplateEntity = KleeneStar.Model.Entities.Template;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// The input of the nodes of a template hierarchy: a tree of references to existing
    /// templates whose objects are created with the object of the root template.
    /// </summary>
    /// <remarks>
    /// The control renders a host the client control <c>kleenestar.core.TemplateHierarchyCtrl</c>
    /// (<c>Assets/js/templatehierarchy.js</c>) builds the tree in, and submits the nodes as one
    /// hidden input in the form of <see cref="TemplateHierarchyNodeList"/>. The host carries what
    /// the tree is edited with, so the client needs no further request:
    /// <list type="bullet">
    ///   <item><c>data-value</c>: the stored nodes of the edited hierarchy, depth first.</item>
    ///   <item><c>data-candidates</c>: the templates that may be added (the active templates of
    ///   the workspace and those of other workspaces whose classes the caller administers) and
    ///   the templates the stored nodes reference, each with the classes it serves.</item>
    ///   <item><c>data-assignees</c>: the active accounts of the operator side.</item>
    ///   <item><c>data-links</c>: the links a node may name (<see cref="TemplateHierarchyNodeList.LinkTypes"/>),
    ///   each relation type with its label and, unless it is symmetric, with its inverse label
    ///   (value <c>{key}:inverse</c>), translated, and whether it blocks the completion of its
    ///   target (<c>blocking</c>, for the cycle check of the client).</item>
    /// </list>
    /// </remarks>
    public sealed class TemplateHierarchyInput : ControlFormItemInput<ControlFormInputValueString>
    {
        /// <summary>
        /// The serializer settings of the data attributes.
        /// </summary>
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Gets or sets the edited hierarchy, computed from the render context; null while one is
        /// created.
        /// </summary>
        public Func<IRenderControlContext, TemplateHierarchy> Hierarchy { get; set; }

        /// <summary>
        /// Gets or sets the workspace the hierarchy belongs to, computed from the render context.
        /// </summary>
        public Func<IRenderControlContext, Workspace> Workspace { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public TemplateHierarchyInput(string id = null)
            : base(id)
        {
        }

        /// <summary>
        /// Converts the control to an HTML representation.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="visualTree">The visual tree representing the control's structure.</param>
        /// <returns>An HTML node representing the rendered control.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var hierarchy = Hierarchy?.Invoke(renderContext);
            var workspace = Workspace?.Invoke(renderContext);
            var stored = hierarchy is null ? [] : CoreHub.TemplateHierarchyManager.GetNodes(hierarchy.Id);

            return new HtmlElementTextContentDiv()
            {
                Id = Id,
                Class = Css.Concatenate("wx-kleenestar-templatehierarchy", Classes)
            }
                .AddUserAttribute("name", Name?.Invoke(renderContext))
                .AddUserAttribute("data-value", TemplateHierarchyNodeList.Serialize(stored))
                .AddUserAttribute("data-candidates", JsonSerializer.Serialize(Candidates(renderContext, workspace, stored), _options))
                .AddUserAttribute("data-assignees", JsonSerializer.Serialize(Assignees(stored), _options))
                .AddUserAttribute("data-links", JsonSerializer.Serialize(Links(renderContext), _options));
        }

        /// <summary>
        /// Creates a value from the specified string representation.
        /// </summary>
        /// <param name="value">The string representation of the value.</param>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The value.</returns>
        protected override ControlFormInputValueString CreateValue(string value, IRenderControlFormContext renderContext)
        {
            return new ControlFormInputValueString(value);
        }

        /// <summary>
        /// Collects the templates a node may reference, and those the stored nodes reference.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <param name="workspace">The workspace of the hierarchy.</param>
        /// <param name="stored">The stored nodes.</param>
        /// <returns>The candidates, the templates of the workspace first, each group by name.</returns>
        private static IEnumerable<object> Candidates(IRenderControlContext renderContext, Workspace workspace, IReadOnlyList<TemplateHierarchyNode> stored)
        {
            var request = renderContext?.Request;
            var referenced = stored.Select(x => x.TemplateId).ToHashSet();
            var templates = CoreHub.TemplateManager
                .GetTemplates(new Query<TemplateEntity>())
                .ToList();
            var classIds = CoreHub.TemplateManager.GetClassIds(templates);
            var classes = CoreHub.ClassManager
                .GetClasses(new Query<ClassEntity>())
                .ToDictionary(x => x.Id);

            // a template of another workspace creates its object there on the hierarchy's
            // authority, so it is offered where the caller administers one of its classes
            bool Offered(TemplateEntity candidate) => candidate.State == TemplateState.Active
                && (candidate.Class?.WorkspaceId == workspace?.Id
                    || classIds[candidate.Id].Any(x => ContentAuthorization.MayAdminister(x, request)));

            return templates
                .Where(x => referenced.Contains(x.Id) || Offered(x))
                .OrderBy(x => x.Class?.WorkspaceId == workspace?.Id ? 0 : 1)
                .ThenBy(x => x.Class?.Workspace?.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = ProseText.ToPlainText(x.Description),
                    Icon = x.Icon?.Uri?.ToString(),
                    Category = TemplateCategories.Normalize(x.Category),
                    Workspace = x.Class?.Workspace?.Name,
                    Local = x.Class?.WorkspaceId == workspace?.Id,
                    Archived = x.State != TemplateState.Active,
                    Offered = Offered(x),
                    Classes = classIds[x.Id]
                        .Select(id => classes.TryGetValue(id, out var c) ? c : null)
                        .Where(c => c is not null)
                        .Select(c => new
                        {
                            Id = c.Id,
                            Name = c.Name,
                            Icon = c.Icon?.Uri?.ToString()
                        })
                });
        }

        /// <summary>
        /// Collects the links a node may name, read from the node: the label of a relation type
        /// for the link from the object of the node to the objects of its targets, the inverse
        /// label for the other direction.
        /// </summary>
        /// <param name="renderContext">The context in which the control is rendered.</param>
        /// <returns>The links in the order of the catalog.</returns>
        private static IEnumerable<object> Links(IRenderControlContext renderContext)
        {
            foreach (var type in TemplateHierarchyNodeList.LinkTypes())
            {
                var blocking = type.Effect == WebExpress.WebApp.WebRelation.RelationEffect.BlocksCompletion;

                yield return new
                {
                    Value = TemplateHierarchyNodeList.FormatLink(type.Key, false),
                    Label = I18N.Translate(renderContext, type.Label),
                    Icon = type.Icon,
                    Blocking = blocking
                };

                if (!type.Symmetric && !string.IsNullOrWhiteSpace(type.InverseLabel))
                {
                    yield return new
                    {
                        Value = TemplateHierarchyNodeList.FormatLink(type.Key, true),
                        Label = I18N.Translate(renderContext, type.InverseLabel),
                        Icon = type.Icon,
                        Blocking = blocking
                    };
                }
            }
        }

        /// <summary>
        /// Collects the accounts a node may be assigned to: the active accounts of the operator
        /// side, and those the stored nodes name already.
        /// </summary>
        /// <param name="stored">The stored nodes.</param>
        /// <returns>The accounts by name.</returns>
        private static IEnumerable<object> Assignees(IReadOnlyList<TemplateHierarchyNode> stored)
        {
            var named = stored.Where(x => x.AssigneeId is not null).Select(x => x.AssigneeId.Value).ToHashSet();

            return CoreHub.IdentityManager
                .GetIdentities(new Query<Identity>())
                .Where(x => named.Contains(x.Id) || (x.State == IdentityState.Active && x.TenantId is null))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new
                {
                    Id = x.Id,
                    Name = x.Name,
                    Position = x.Position,
                    Avatar = x.Avatar?.Uri?.ToString()
                });
        }
    }
}
