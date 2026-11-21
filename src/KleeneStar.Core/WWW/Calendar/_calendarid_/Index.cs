using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebUri;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Calendar._calendarid_
{
    /// <summary>
    /// Details page for a single <see cref="Model.Entities.Calendar"/>.
    /// </summary>
    [WebIcon<IconCalendar>]
    [Title("kleenestar.core:calendar.detail.title")]
    [CalendarIdSegment]
    [Scope<IScopeGeneral>]
    [Domain<Model.Entities.Calendar>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The render context.</param>
        /// <param name="visualTree">The visual tree.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var calendarParameter = renderContext.Request.GetParameter<CalendarIdParameter>();
            var guid = Guid.TryParse(calendarParameter?.Value, out var id) ? id : Guid.Empty;
            var calendar = CoreHub.CalendarManager.GetCalendar(guid);
            var @class = calendar?.Class;
            var workspace = @class?.Workspace;

            var uri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = workspace?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Issues._workspacekey_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new UriPathSegmentConstant("classes")
                {
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Classes._workspacekey_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new ClassIdUriPathSegmentVariable<ClassIdParameter>()
                {
                    Value = @class?.Id.ToString(),
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Class._classid_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(new ClassIdParameter(@class?.Id ?? Guid.Empty))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new UriPathSegmentConstant("calendars")
                {
                    Uri = CoreHub.GetUri<global::KleeneStar.Core.WWW.Calendars._classid_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                        .BindParameters(new ClassIdParameter(@class?.Id ?? Guid.Empty))
                        .BindParameters(renderContext.Request)
                })
                .Concat(new CalendarIdUriPathSegmentVariable<CalendarIdParameter>()
                {
                    Value = calendar?.Id.ToString(),
                    Uri = renderContext.Request.Uri
                })
                .ToUri()
                .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                .BindParameters(new ClassIdParameter(@class?.Id ?? Guid.Empty))
                .BindParameters(renderContext.Request);

            visualTree.BreadcrumbUri = uri;
            visualTree.Title ??= calendar?.Name;
            visualTree.Content.MainPanel.Headline.Title = calendar?.Name;
        }
    }
}
