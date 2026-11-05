using UnityEngine;
using UnityEngine.InputSystem;

namespace CraneTactics
{
    public sealed class CraneInput
    {
        public Vector2 Pointer => Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
        public bool Action => (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
        public bool Pause => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        public bool KeyboardAction => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public bool PointerMoved => Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude>0;
        public Vector2 Movement
        {
            get
            {
                var k=Keyboard.current; if(k==null) return Vector2.zero;
                return new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed || k.leftArrowKey.isPressed?1:0),(k.wKey.isPressed || k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed || k.downArrowKey.isPressed?1:0)).normalized;
            }
        }
    }
}
