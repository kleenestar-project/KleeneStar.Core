using System;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Core.WebControl
{
    /// <summary>
    /// Represents a master-detail view with a REST-backed list on the master side and a
    /// detail frame for workspace data.
    /// </summary>
    /// <remarks>
    /// The composite is the single owner of the selection: list items carry an
    /// <see cref="ActionMasterDetail"/> targeting <see cref="ControlId"/> rather than writing
    /// into the frame themselves, so the selected entry stays highlighted, the detail side can
    /// be closed and reopened, and the view collapses into the sequential single-column mode on
    /// narrow containers.
    /// </remarks>
    public class ListDetailControl : ControlMasterDetail
    {
        /// <summary>
        /// Represents the unique identifier of the master-detail control itself. It is the
        /// target the list items address with their <see cref="ActionMasterDetail"/>.
        /// </summary>
        public static readonly string ControlId = "id_4F2C9E7A1B8D40A6B3E5C1D7F9A2B6C4";

        /// <summary>
        /// Represents the unique identifier for the frame used in this context.
        /// </summary>
        public static readonly string FrameId = "id_D85AD7256B374857914E70938DFA1F81";

        /// <summary>
        /// Represents the unique identifier for the list used in this context.
        /// </summary>
        public static readonly string ListId = "id_8C7E2A1B4D6F49A2B5C3E8F1A7D9B4E2";

        /// <summary>
        /// Gets or sets the delegate used to generate a RESTful URI based on the provided rendering context.
        /// When set, the inner <see cref="List"/> control receives a <see cref="ControlDataList.ServiceFactory"/>
        /// descriptor that queries this URI. Clears the descriptor when set to <see langword="null"/>.
        /// </summary>
        public Func<IRenderControlContext, IUri> RestUri
        {
            get => renderContext => InnerRestUri?.Invoke(renderContext);
            set => InnerRestUri = value;
        }

        private Func<IRenderControlContext, IUri> InnerRestUri { get; set; }

        /// <summary>
        /// Gets or sets the data service descriptor for the inner <see cref="List"/> control.
        /// Mirrors the property on <see cref="ControlDataList"/>; setting it routes the data
        /// service straight to the list without going through <see cref="RestUri"/>.
        /// </summary>
        public Func<IRenderControlContext, DataServiceDescriptor> ServiceFactory
        {
            get => List.ServiceFactory;
            set => List.ServiceFactory = value;
        }

        /// <summary>
        /// Gets or sets the delegate used to create a binding for a given render control context.
        /// </summary>
        public Func<IRenderControlContext, IBinding> Bind { get; set; }

        /// <summary>
        /// Gets the configuration tile that provides REST access to
        /// workspace data.
        /// </summary>
        public ControlDataList List { get; } = new ControlDataList(ListId)
        {
            Selectable = _ => true,
            Sortable = _ => true,
            // the caption of the list, in the reader's language like the view switch naming it
            Title = _ => "kleenestar.core:view.list.title"
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The unique identifier for the control.</param>
        public ListDetailControl(string id = null)
            : base(id ?? ControlId)
        {
            List.ServiceFactory = renderContext => DataServiceDescriptor.QueryData(RestUri?.Invoke(renderContext)?.ToString());
            List.Bind = renderContext => Bind?.Invoke(renderContext);

            // the detail endpoints are regular pages of the application, so the frame embeds
            // only their main content region instead of the whole document
            Detail = new ControlFrame(FrameId)
            {
                Selector = _ => "#wx-content-main"
            };

            AddMaster(List);

            MasterInitialSize = _ => 250;
            Unit = _ => TypeSizeUnit.Pixel;

            // the two columns scroll independently, which needs a definite height, and the
            // control brings one of its own (70vh) for a host that has none. Here the
            // composite is the view rather than one block on a page of them, so it takes the
            // height the shell offers instead: the WebApp content panel turns the chain of
            // panels between itself and a filling region into a flex column on its own, so
            // declaring Fill is all this side has to do - no percentage against an
            // auto-height parent, no viewport arithmetic under it.
            //
            // Fill stays an opt-in of the control, and a host that hands nothing down leaves
            // the region at the 70vh fallback rather than at its content. A composite that
            // is not the view opts back out - see ObjectHistoryListFragment, which is copied
            // into a modal and sizes itself.
            Fill = _ => true;
        }
    }
}
