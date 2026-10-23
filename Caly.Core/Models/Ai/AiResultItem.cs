// Copyright (c) BobLd
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Avalonia;

namespace Caly.Core.Models.Ai;

public enum AiItemKind : byte
{
    Table = 0,
    Chart = 1,

    /// <summary>
    /// A reply that could not be read as table or chart data, kept as it came.
    /// </summary>
    Text = 2
}

/// <summary>
/// A rectangle on a page, in 0..1 of the page's displayed size, origin at the top left.
/// </summary>
public readonly record struct AiBox(double X0, double Y0, double X1, double Y1)
{
    /// <summary>
    /// The box on a page displayed at <paramref name="pageSize"/>. Both the picture the model
    /// saw and the page layer have their origin at the top left, so nothing is flipped.
    /// </summary>
    public Rect ToRect(Size pageSize)
        => new(X0 * pageSize.Width, Y0 * pageSize.Height, (X1 - X0) * pageSize.Width, (Y1 - Y0) * pageSize.Height);
}

/// <summary>
/// A box as a page draws it. <paramref name="ItemIndex"/> is the item's index in the AI panel's list.
/// </summary>
public readonly record struct AiPageBox(AiBox Box, int ItemIndex, bool IsSelected);

public sealed record AiResultItem(AiItemKind Kind, int PageNumber, AiBox? Box, string Content);
