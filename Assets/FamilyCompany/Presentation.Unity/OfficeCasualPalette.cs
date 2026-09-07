using UnityEngine;

namespace FamilyCompany.Presentation.Unity
{
    /// <summary>Shared by native uGUI menus and IMGUI shop/market; never colours the world.</summary>
    public static class OfficeCasualPalette
    {
        public static readonly Color Ink = new Color32(0x30, 0x3D, 0x42, 255);
        public static readonly Color Muted = new Color32(0x69, 0x73, 0x6E, 255);
        public static readonly Color Paper = new Color32(0xFA, 0xF9, 0xF4, 255);
        public static readonly Color Surface = Color.white;
        public static readonly Color Line = new Color32(0xDC, 0xE3, 0xD9, 255);
        public static readonly Color Sage = new Color32(0x41, 0x6F, 0x56, 255);
        public static readonly Color SageLight = new Color32(0xE1, 0xEB, 0xE2, 255);
        public static readonly Color Apricot = new Color32(0xF2, 0xE4, 0xCF, 255);
        public static readonly Color Danger = new Color32(0xAC, 0x57, 0x49, 255);
    }
}
