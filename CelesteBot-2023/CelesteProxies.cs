﻿using Monocle;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CelesteBot_2023
{
    public static class CelesteProxies
    {
        // Need to change some methods/fields to public
        public readonly static Type t_MInput = typeof(MInput);

        public readonly static MethodInfo m_UpdateVirtualInputs = t_MInput.GetMethod("UpdateVirtualInputs", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        [CelesteProxy("System.Void Monocle.MInput::UpdateVirtualInputs()")]
        public static void MInput_UpdateVirtualInputs() => m_UpdateVirtualInputs.GetFastDelegate().Invoke(null);
    }
    public class CelesteProxyAttribute : Attribute
    {
        public string FindableID;
        public CelesteProxyAttribute(string ID)
        {
            FindableID = ID;
        }
    }
}
