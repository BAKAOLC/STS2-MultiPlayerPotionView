using STS2MultiPlayerPotionView.Patching.Core;

namespace STS2MultiPlayerPotionView.Patching.Models
{
    /// <summary>
    ///     Interface for a class that contains patches for the mod.
    ///     This is used to group patches together and make it easier to add them to the patcher.
    /// </summary>
    public interface IModPatches
    {
        /// <summary>
        ///     Adds the patches to the patcher.
        /// </summary>
        static abstract void AddTo(ModPatcher patcher);
    }
}
