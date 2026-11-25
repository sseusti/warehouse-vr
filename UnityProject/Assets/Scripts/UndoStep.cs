using System;

namespace Warehouse
{
    /// <summary>One remote action that <see cref="UndoHistory"/> can take back.</summary>
    public sealed class UndoStep
    {
        readonly Action revert;
        readonly Func<bool> canRevert;

        /// <param name="label">Short name for the hint panel, for example "размер гири".</param>
        /// <param name="owner">Object the action changed; used to forget its steps.</param>
        /// <param name="revert">Restores the state from before the action.</param>
        /// <param name="canRevert">Optional check, for example that there is room to grow back.</param>
        /// <param name="fadesItself">True when the revert darkens the view on its own, like a player size change.</param>
        public UndoStep(string label, object owner, Action revert, Func<bool> canRevert = null, bool fadesItself = false)
        {
            Label = label;
            Owner = owner;
            this.revert = revert;
            this.canRevert = canRevert;
            FadesItself = fadesItself;
        }

        public string Label { get; }
        public object Owner { get; }
        public bool FadesItself { get; }
        public bool CanRevert => canRevert == null || canRevert();

        public void Revert() => revert();
    }
}
