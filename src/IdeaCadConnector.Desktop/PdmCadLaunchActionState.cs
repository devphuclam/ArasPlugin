using System;
using IdeaCadConnector.Core.Localization;

namespace IdeaCadConnector.Desktop
{
    internal enum PdmCadLaunchMode
    {
        Hidden,
        Unavailable,
        CheckoutAndOpen,
        OpenCheckedOut,
        OpenReadOnly
    }

    internal sealed class PdmCadLaunchActionContext
    {
        public bool HasSelection { get; set; }
        public bool IsRootAssembly { get; set; }
        public bool HasPrimaryCad { get; set; }
        public bool IsConnected { get; set; }
        public bool HasLiveCadId { get; set; }
        public bool HasLifecycleState { get; set; }
        public bool CanCheckout { get; set; }
        public bool HasValidLocalCheckout { get; set; }
        public bool IsLockedByOther { get; set; }
        public bool HasNativeFile { get; set; }
        public bool IsBusy { get; set; }
    }

    internal sealed class PdmCadLaunchActionState
    {
        private const string UnavailableLabelKey = TranslationKeys.PdmCadLaunchUnavailable;

        private PdmCadLaunchActionState(
            PdmCadLaunchMode mode,
            bool isVisible,
            bool isEnabled,
            string labelKey,
            string disabledReasonKey = null)
        {
            Mode = mode;
            IsVisible = isVisible;
            IsEnabled = isEnabled;
            LabelKey = labelKey;
            DisabledReasonKey = disabledReasonKey;
        }

        public PdmCadLaunchMode Mode { get; }
        public bool IsVisible { get; }
        public bool IsEnabled { get; }
        public string LabelKey { get; }
        public string DisabledReasonKey { get; }

        public static PdmCadLaunchActionState Create(PdmCadLaunchActionContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (!context.HasSelection || context.IsRootAssembly || !context.HasPrimaryCad)
                return new PdmCadLaunchActionState(PdmCadLaunchMode.Hidden, false, false, UnavailableLabelKey);

            if (context.IsBusy)
                return Unavailable(TranslationKeys.PdmCadLaunchBusy);
            if (!context.IsConnected)
                return Unavailable(TranslationKeys.PdmCadLaunchConnectToAras);
            if (!context.HasLiveCadId)
                return Unavailable(TranslationKeys.PdmCadLaunchRefreshCad);
            if (!context.HasLifecycleState)
                return Unavailable(TranslationKeys.PdmCadLaunchRefreshState);

            if (context.HasValidLocalCheckout)
                return Enabled(PdmCadLaunchMode.OpenCheckedOut, TranslationKeys.PdmOpenCheckedOutIronCad);

            if (context.IsLockedByOther)
            {
                return context.HasNativeFile
                    ? Enabled(PdmCadLaunchMode.OpenReadOnly, TranslationKeys.PdmOpenIronCadReadOnly)
                    : Unavailable(TranslationKeys.PdmCadLaunchNoReadableFile);
            }

            if (context.CanCheckout)
                return Enabled(PdmCadLaunchMode.CheckoutAndOpen, TranslationKeys.PdmCheckoutAndOpenIronCad);

            return context.HasNativeFile
                ? Enabled(PdmCadLaunchMode.OpenReadOnly, TranslationKeys.PdmOpenIronCadReadOnly)
                : Unavailable(TranslationKeys.PdmCadLaunchNoReadableFile);
        }

        private static PdmCadLaunchActionState Enabled(PdmCadLaunchMode mode, string labelKey)
        {
            return new PdmCadLaunchActionState(mode, true, true, labelKey);
        }

        private static PdmCadLaunchActionState Unavailable(string reasonKey)
        {
            return new PdmCadLaunchActionState(
                PdmCadLaunchMode.Unavailable,
                true,
                false,
                UnavailableLabelKey,
                reasonKey);
        }
    }
}
