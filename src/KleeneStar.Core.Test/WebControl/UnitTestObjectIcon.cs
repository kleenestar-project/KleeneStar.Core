using KleeneStar.Core.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.Test.WebControl
{
    /// <summary>
    /// Provides unit tests for <see cref="ObjectIcon"/> — the picture an object is shown under
    /// in every list, table, tile and tree.
    /// </summary>
    /// <remarks>
    /// The one thing worth guarding is that the icon an object carries never wins over its
    /// class. <c>Object.Icon</c> is written once at creation - the generated mark for an object
    /// created through the UI, a copy of the class icon for a seeded one - and never updated,
    /// so both are stale by construction and the copy is the one that looks right until the
    /// class avatar changes.
    /// </remarks>
    public class UnitTestObjectIcon
    {
        /// <summary>
        /// Builds an object carrying the icon that <c>CoreHub.GenerateIcon</c> would have
        /// written for it — the file is named after the record's own id.
        /// </summary>
        /// <param name="class">The class of the object.</param>
        /// <returns>The object.</returns>
        private static Model.Entities.Object Generated(Model.Entities.Class @class)
        {
            var id = Guid.NewGuid();

            return new Model.Entities.Object(id)
            {
                Icon = ImageIcon.FromString($"/kleenestar/assets/icons/{id}.svg"),
                ClassId = @class?.Id ?? Guid.Empty,
                Class = @class
            };
        }

        /// <summary>
        /// Verifies the case the surfaces were getting wrong: an object that carries only its
        /// generated icon is shown under the icon of its class.
        /// </summary>
        [Fact]
        public void Resolve_YieldsTheClassIcon()
        {
            var @class = new Model.Entities.Class { Icon = ImageIcon.FromString("/kleenestar/assets/icons/ticket.svg") };
            var @object = Generated(@class);

            Assert.Equal("/kleenestar/assets/icons/ticket.svg", ObjectIcon.Resolve(@object)?.Uri?.ToString());
            Assert.Equal("/kleenestar/assets/icons/ticket.svg", ObjectIcon.Uri(@object));
        }

        /// <summary>
        /// Verifies the case the user actually hit: a seeded object carries a <em>copy</em> of
        /// the icon its class had at the time, and the class has been given a new avatar since.
        /// The copy must not win, or every object of the class goes on showing the old picture.
        /// </summary>
        [Fact]
        public void Resolve_StaleCopyOfTheClassIcon_LosesToTheClass()
        {
            var @class = new Model.Entities.Class { Icon = ImageIcon.FromString("/kleenestar/assets/icons/1cf19c71-9ccc3b21.png") };

            var @object = new Model.Entities.Object(Guid.NewGuid())
            {
                // what KleeneStarDbSeeder.Objects wrote: the class icon as it was back then
                Icon = ImageIcon.FromString("/kleenestar/assets/icons/doc.svg"),
                ClassId = @class.Id,
                Class = @class
            };

            Assert.Equal("/kleenestar/assets/icons/1cf19c71-9ccc3b21.png", ObjectIcon.Resolve(@object)?.Uri?.ToString());
        }

        /// <summary>
        /// Verifies that a class without a picture leaves the object with the one it carries
        /// rather than with nothing — a row is better identified by a tinted mark than by a
        /// blank.
        /// </summary>
        [Fact]
        public void Resolve_ClassWithoutIcon_KeepsTheObjectIcon()
        {
            var @class = new Model.Entities.Class();
            var @object = Generated(@class);

            Assert.Equal(@object.Icon.Uri.ToString(), ObjectIcon.Resolve(@object)?.Uri?.ToString());
        }

        /// <summary>
        /// Verifies the fallback overload: a glyph stands in where neither the object nor its
        /// class carries a picture, and never overrules one that does.
        /// </summary>
        [Fact]
        public void Resolve_WithFallback_UsesTheGlyphOnlyWhenNothingElseIsThere()
        {
            var fallback = new IconFileLines();

            var bare = new Model.Entities.Object(Guid.NewGuid()) { Class = new Model.Entities.Class() };
            Assert.Same(fallback, ObjectIcon.Resolve(bare, fallback));

            Assert.Same(fallback, ObjectIcon.Resolve(null, fallback));

            var @class = new Model.Entities.Class { Icon = ImageIcon.FromString("/kleenestar/assets/icons/doc.svg") };
            Assert.NotSame(fallback, ObjectIcon.Resolve(Generated(@class), fallback));
        }

        /// <summary>
        /// Verifies that a null object answers null rather than throwing — a list may hold a
        /// row whose record has gone.
        /// </summary>
        [Fact]
        public void Resolve_NullObject_IsNull()
        {
            Assert.Null(ObjectIcon.Resolve(null));
            Assert.Null(ObjectIcon.Uri(null));
        }
    }
}
