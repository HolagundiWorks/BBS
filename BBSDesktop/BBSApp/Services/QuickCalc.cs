// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Human Centric Works, Hospet

using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BBSApp.Services;

/// <summary>Keyboard-only expression evaluator for the floating calculator.</summary>
public static class QuickCalc
{
    private static readonly Regex SafeExpr = new(
        @"^\s*[0-9+\-*/().%\s]+\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryEvaluate(string? expression, out string result)
    {
        result = "";
        if (string.IsNullOrWhiteSpace(expression)) return false;

        var expr = expression.Trim()
            .Replace('×', '*')
            .Replace('÷', '/')
            .Replace(',', '.'); // locale-friendly decimal

        if (!SafeExpr.IsMatch(expr))
        {
            result = "";
            return false;
        }

        try
        {
            var value = new DataTable().Compute(expr, null);
            if (value is null or DBNull)
            {
                result = "";
                return false;
            }
            var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                result = "";
                return false;
            }
            result = Format(d);
            return true;
        }
        catch
        {
            result = "";
            return false;
        }
    }

    private static string Format(double d)
    {
        if (Math.Abs(d - Math.Round(d)) < 1e-12 && Math.Abs(d) < 1e15)
            return Math.Round(d).ToString(CultureInfo.InvariantCulture);
        return d.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
