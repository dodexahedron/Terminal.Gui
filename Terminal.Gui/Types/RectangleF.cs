﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Copied from https://github.com/dotnet/corefx/tree/master/src/System.Drawing.Primitives/src/System/Drawing

using System;
using System.ComponentModel;

namespace Terminal.Gui; 

/// <summary>
/// Stores the location and size of a rectangular region.
/// </summary>
public struct RectangleF : IEquatable<RectangleF> {
	/// <summary>
	/// Initializes a new instance of the <see cref='Terminal.Gui.RectangleF'/> class.
	/// </summary>
	public static readonly RectangleF Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref='Terminal.Gui.RectangleF'/> class with the specified location
	/// and size.
	/// </summary>
	public RectangleF (float x, float y, float width, float height)
	{
		X = x;
		Y = y;
		Width = width;
		Height = height;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref='Terminal.Gui.RectangleF'/> class with the specified location
	/// and size.
	/// </summary>
	public RectangleF (PointF location, SizeF size)
	{
		X = location.X;
		Y = location.Y;
		Width = size.Width;
		Height = size.Height;
	}

	/// <summary>
	/// Creates a new <see cref='Terminal.Gui.RectangleF'/> with the specified location and size.
	/// </summary>
	public static RectangleF FromLTRB (float left, float top, float right, float bottom) =>
		new (left, top, right - left, bottom - top);

	/// <summary>
	/// Gets or sets the coordinates of the upper-left corner of the rectangular region represented by this
	/// <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	[Browsable (false)]
	public PointF Location {
		get => new (X, Y);
		set {
			X = value.X;
			Y = value.Y;
		}
	}

	/// <summary>
	/// Gets or sets the size of this <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	[Browsable (false)]
	public SizeF Size {
		get => new (Width, Height);
		set {
			Width = value.Width;
			Height = value.Height;
		}
	}

	/// <summary>
	/// Gets or sets the x-coordinate of the upper-left corner of the rectangular region defined by this
	/// <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	public float X { get; set; }

	/// <summary>
	/// Gets or sets the y-coordinate of the upper-left corner of the rectangular region defined by this
	/// <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	public float Y { get; set; }

	/// <summary>
	/// Gets or sets the width of the rectangular region defined by this <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	public float Width { get; set; }

	/// <summary>
	/// Gets or sets the height of the rectangular region defined by this <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	public float Height { get; set; }

	/// <summary>
	/// Gets the x-coordinate of the upper-left corner of the rectangular region defined by this
	/// <see cref='Terminal.Gui.RectangleF'/> .
	/// </summary>
	[Browsable (false)]
	public float Left => X;

	/// <summary>
	/// Gets the y-coordinate of the upper-left corner of the rectangular region defined by this
	/// <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	[Browsable (false)]
	public float Top => Y;

	/// <summary>
	/// Gets the x-coordinate of the lower-right corner of the rectangular region defined by this
	/// <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	[Browsable (false)]
	public float Right => X + Width;

	/// <summary>
	/// Gets the y-coordinate of the lower-right corner of the rectangular region defined by this
	/// <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	[Browsable (false)]
	public float Bottom => Y + Height;

	/// <summary>
	/// Tests whether this <see cref='Terminal.Gui.RectangleF'/> has a <see cref='Terminal.Gui.RectangleF.Width'/> or a
	/// <see cref='Terminal.Gui.RectangleF.Height'/> of 0.
	/// </summary>
	[Browsable (false)]
	public bool IsEmpty => Width <= 0 || Height <= 0;

	/// <summary>
	/// Tests whether <paramref name="obj"/> is a <see cref='Terminal.Gui.RectangleF'/> with the same location and
	/// size of this <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	public override bool Equals (object obj) => obj is RectangleF && Equals ((RectangleF)obj);

	/// <summary>
	/// Returns true if two <see cref='Terminal.Gui.RectangleF'/> objects have equal location and size.
	/// </summary>
	/// <param name="other"></param>
	/// <returns></returns>
	public bool Equals (RectangleF other) => this == other;

	/// <summary>
	/// Tests whether two <see cref='Terminal.Gui.RectangleF'/> objects have equal location and size.
	/// </summary>
	public static bool operator == (RectangleF left, RectangleF right) =>
		left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;

	/// <summary>
	/// Tests whether two <see cref='Terminal.Gui.RectangleF'/> objects differ in location or size.
	/// </summary>
	public static bool operator != (RectangleF left, RectangleF right) => !(left == right);

	/// <summary>
	/// Determines if the specified point is contained within the rectangular region defined by this
	/// <see cref='Terminal.Gui.Rect'/> .
	/// </summary>
	public bool Contains (float x, float y) => X <= x && x < X + Width && Y <= y && y < Y + Height;

	/// <summary>
	/// Determines if the specified point is contained within the rectangular region defined by this
	/// <see cref='Terminal.Gui.Rect'/> .
	/// </summary>
	public bool Contains (PointF pt) => Contains (pt.X, pt.Y);

	/// <summary>
	/// Determines if the rectangular region represented by <paramref name="rect"/> is entirely contained within
	/// the rectangular region represented by this <see cref='Terminal.Gui.Rect'/> .
	/// </summary>
	public bool Contains (RectangleF rect) =>
		X <= rect.X && rect.X + rect.Width <= X + Width && Y <= rect.Y && rect.Y + rect.Height <= Y + Height;

	/// <summary>
	/// Gets the hash code for this <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	public override int GetHashCode () => Height.GetHashCode () + Width.GetHashCode () ^ X.GetHashCode () + Y.GetHashCode ();

	/// <summary>
	/// Inflates this <see cref='Terminal.Gui.Rect'/> by the specified amount.
	/// </summary>
	public void Inflate (float x, float y)
	{
		X -= x;
		Y -= y;
		Width += 2 * x;
		Height += 2 * y;
	}

	/// <summary>
	/// Inflates this <see cref='Terminal.Gui.Rect'/> by the specified amount.
	/// </summary>
	public void Inflate (SizeF size) => Inflate (size.Width, size.Height);

	/// <summary>
	/// Creates a <see cref='Terminal.Gui.Rect'/> that is inflated by the specified amount.
	/// </summary>
	public static RectangleF Inflate (RectangleF rect, float x, float y)
	{
		var r = rect;
		r.Inflate (x, y);
		return r;
	}

	/// <summary>
	/// Creates a Rectangle that represents the intersection between this Rectangle and rect.
	/// </summary>
	public void Intersect (RectangleF rect)
	{
		var result = Intersect (rect, this);

		X = result.X;
		Y = result.Y;
		Width = result.Width;
		Height = result.Height;
	}

	/// <summary>
	/// Creates a rectangle that represents the intersection between a and b. If there is no intersection, an
	/// empty rectangle is returned.
	/// </summary>
	public static RectangleF Intersect (RectangleF a, RectangleF b)
	{
		var x1 = Math.Max (a.X, b.X);
		var x2 = Math.Min (a.X + a.Width, b.X + b.Width);
		var y1 = Math.Max (a.Y, b.Y);
		var y2 = Math.Min (a.Y + a.Height, b.Y + b.Height);

		if (x2 >= x1 && y2 >= y1) {
			return new RectangleF (x1, y1, x2 - x1, y2 - y1);
		}

		return Empty;
	}

	/// <summary>
	/// Determines if this rectangle intersects with rect.
	/// </summary>
	public bool IntersectsWith (RectangleF rect) =>
		rect.X < X + Width && X < rect.X + rect.Width && rect.Y < Y + Height && Y < rect.Y + rect.Height;

	/// <summary>
	/// Creates a rectangle that represents the union between a and b.
	/// </summary>
	public static RectangleF Union (RectangleF a, RectangleF b)
	{
		var x1 = Math.Min (a.X, b.X);
		var x2 = Math.Max (a.X + a.Width, b.X + b.Width);
		var y1 = Math.Min (a.Y, b.Y);
		var y2 = Math.Max (a.Y + a.Height, b.Y + b.Height);

		return new RectangleF (x1, y1, x2 - x1, y2 - y1);
	}

	/// <summary>
	/// Adjusts the location of this rectangle by the specified amount.
	/// </summary>
	public void Offset (PointF pos) => Offset (pos.X, pos.Y);

	/// <summary>
	/// Adjusts the location of this rectangle by the specified amount.
	/// </summary>
	public void Offset (float x, float y)
	{
		X += x;
		Y += y;
	}

	/// <summary>
	/// Converts the specified <see cref='Terminal.Gui.Rect'/> to a
	/// <see cref='Terminal.Gui.RectangleF'/>.
	/// </summary>
	public static implicit operator RectangleF (Rect r) => new (r.X, r.Y, r.Width, r.Height);

	/// <summary>
	/// Converts the <see cref='Terminal.Gui.RectangleF.Location'/> and <see cref='Terminal.Gui.RectangleF.Size'/>
	/// of this <see cref='Terminal.Gui.RectangleF'/> to a human-readable string.
	/// </summary>
	public override string ToString () =>
		"{X=" + X + ",Y=" + Y +
		",Width=" + Width + ",Height=" + Height + "}";
}