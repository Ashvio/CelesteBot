﻿using System;
using Celeste.Mod;
using MonoMod.Utils;

namespace CelesteBot_2023.SimplifiedGraphics;

internal static class ExtendedVariantsUtils
{
    private static readonly Lazy<EverestModule> module = new(() => ModUtils.GetModule("ExtendedVariantMode"));
    private static readonly Lazy<object> triggerManager = new(() => module.Value?.GetFieldValue<object>("TriggerManager"));
    private static readonly Lazy<object> variantHandlers = new(() => module.Value?.GetFieldValue<object>("VariantHandlers"));

    private static readonly Lazy<FastReflectionDelegate> getCurrentVariantValue = new(() =>
        triggerManager.Value?.GetType().GetMethodInfo("GetCurrentVariantValue")?.GetFastDelegate());
    private static readonly Lazy<FastReflectionDelegate> setVariantValue = new(() =>
        module.Value?.GetType().Assembly.GetType("ExtendedVariants.UI.ModOptionsEntries").GetMethodInfo("SetVariantValue")?.GetFastDelegate());
    private static readonly Lazy<FastReflectionDelegate> dictionareGetItem = new(() =>
        variantHandlers.Value?.GetType().GetMethodInfo("get_Item")?.GetFastDelegate());

    private static readonly Lazy<Type> variantType =
        new(() => module.Value?.GetType().Assembly.GetType("ExtendedVariants.Module.ExtendedVariantsModule+Variant"));

    // enum value might be different between different ExtendedVariantMode version, so we have to parse from string
    private static readonly Lazy<object> upsideDownVariant = new(ParseVariant("UpsideDown"));
    private static readonly Lazy<object> superDashingVariant = new(ParseVariant("SuperDashing"));
    private static readonly object[] parameterless = { };

    public static Func<object> ParseVariant(string value)
    {
        return () => {
            try
            {
                return variantType.Value == null ? null : Enum.Parse(variantType.Value, value);
            }
            catch (Exception e)
            {
                CutsceneManager.Log($"Parsing Variant.{value} Failed." + e.Message, LogLevel.Error);
                return null;
            }
        };
    }

    public static bool UpsideDown => GetCurrentVariantValue(upsideDownVariant) is { } value && (bool)value;
    public static bool SuperDashing => GetCurrentVariantValue(superDashingVariant) is { } value && (bool)value;

    public static Type GetVariantType(Lazy<object> variant)
    {
        if (variant.Value is null) return null;
        object handler = dictionareGetItem.Value?.Invoke(variantHandlers.Value, variant.Value);
        return (Type)handler?.GetType().GetMethodInfo("GetVariantType")?.Invoke(handler, parameterless);
    }

    public static object GetCurrentVariantValue(Lazy<object> variant)
    {
        if (variant.Value is null) return null;
        return getCurrentVariantValue.Value?.Invoke(triggerManager.Value, variant.Value);
    }

    public static void SetVariantValue(Lazy<object> variant, object value)
    {
        if (variant.Value is null) return;
        setVariantValue.Value?.Invoke(null, variant.Value, value);
    }
}