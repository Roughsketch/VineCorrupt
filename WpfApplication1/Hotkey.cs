using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
namespace VineCorrupt
{
    [Serializable]
    class Hotkey
    {
        private int m_modifiers;
        private List<Key> m_keys;
        private string m_original;

        public struct KeyModifier
        {
            public static int None = 0x00;
            public static int Alt = 0x01;
            public static int Ctrl = 0x02;
            public static int Shift = 0x04;
            public static int Windows = 0x08;
        }

        public Hotkey(string hotkey)
        {
            m_original = hotkey;
            m_keys = new List<Key>();

            hotkey = hotkey.Replace(" ", "");

            if(hotkey != "")
            {
                foreach(var h in hotkey.Split('+'))
                {
                    m_keys.Add((Key)Enum.Parse(typeof(Key), h));
                }
            }
            else
            {
                m_modifiers = KeyModifier.None;
            }
        }

        public string Original()
        {
            return m_original;
        }

        public List<Key> Key()
        {
            return m_keys;
        }

        public int Modifiers()
        {
            return m_modifiers;
        }

        public bool IsActive()
        {
            if (m_keys.Count == 0)
            {
                return false;
            }

            if ((m_modifiers & Hotkey.KeyModifier.Alt) > 0 && (Keyboard.Modifiers & ModifierKeys.Alt) <= 0 ||
                (m_modifiers & Hotkey.KeyModifier.Ctrl) > 0 && (Keyboard.Modifiers & ModifierKeys.Control) <= 0 ||
                (m_modifiers & Hotkey.KeyModifier.Shift) > 0 && (Keyboard.Modifiers & ModifierKeys.Shift) <= 0 ||
                (m_modifiers & Hotkey.KeyModifier.Windows) > 0 && (Keyboard.Modifiers & ModifierKeys.Windows) <= 0)
            {
                return false;
            }

            foreach(var key in m_keys)
            {
                if (Keyboard.IsKeyDown(key) == false)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
