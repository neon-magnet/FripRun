using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FlipRun.Game
{
    /// <summary>入力の窓口。テストでは差し替える。</summary>
    public interface IFlipInput
    {
        /// <summary>スペース / 左クリック / 画面タップ（このフレームに押された）。</summary>
        bool FlipPressed();
        /// <summary>R（リザルト画面でリトライ）。</summary>
        bool RetryPressed();
        /// <summary>M（ミュート切替）。</summary>
        bool MutePressed();
    }

    /// <summary>新しい Input System で読む。</summary>
    public sealed class InputSystemFlipInput : IFlipInput
    {
        public bool FlipPressed()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame) return true;
            // ミュートボタンなど UI の上でのクリック／タップは反転にしない
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !overUi) return true;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame && !overUi) return true;
            return false;
        }

        public bool MutePressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.mKey.wasPressedThisFrame;
        }

        public bool RetryPressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb.rKey.wasPressedThisFrame;
        }
    }

    /// <summary>何も押さない入力（テスト用）。</summary>
    public sealed class NoInput : IFlipInput
    {
        public bool FlipPressed() => false;
        public bool RetryPressed() => false;
        public bool MutePressed() => false;
    }
}
