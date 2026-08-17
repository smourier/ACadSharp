using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ACadSharp.Attributes;
using ACadSharp.Extensions;
using ACadSharp.Objects;
using ACadSharp.Tables;
using ACadSharp.XData;

namespace ACadSharp;

/// <summary>
/// Represents an element in a CadDocument.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public abstract class CadObject : IHandledCadObject
{
	/// <summary>
	/// Document where this element belongs.
	/// </summary>
	public CadDocument Document { get; private set; }

	/// <remarks>
	/// Created on first access, most objects carry no extended data.
	/// </remarks>
	public ExtendedDataDictionary ExtendedData
	{
		get { return this._extendedData ??= new ExtendedDataDictionary(this); }
	}

	/// <inheritdoc/>
	/// <remarks>
	/// If the value is 0 the object is not assigned to a document or a parent.
	/// </remarks>
	[DxfCodeValue(5)]
	public ulong Handle { get; internal set; }

	/// <summary>
	/// Flag that indicates if this object has a dynamic dxf sublcass.
	/// </summary>
	public virtual bool HasDynamicSubclass { get { return false; } }

	/// <summary>
	/// The CAD class name of an object.
	/// </summary>
	public virtual string ObjectName { get; }

	/// <summary>
	/// Get the object type.
	/// </summary>
	public abstract ObjectType ObjectType { get; }

	/// <summary>
	/// Soft-pointer ID/handle to owner object.
	/// </summary>
	[DxfCodeValue(DxfReferenceType.Handle, 330)]
	public IHandledCadObject Owner { get; internal set; }

	/// <summary>
	/// Objects that are attached to this object.
	/// </summary>
	public IEnumerable<CadObject> Reactors
	{
		get
		{
			return this._reactors ?? Enumerable.Empty<CadObject>();
		}
	}

	/// <summary>
	/// Object Subclass marker.
	/// </summary>
	public abstract string SubclassMarker { get; }

	/// <summary>
	/// Extended Dictionary object.
	/// </summary>
	/// <remarks>
	/// An extended dictionary can be created using <see cref="CreateExtendedDictionary"/>.
	/// </remarks>
	public CadDictionary XDictionary
	{
		get { return this._xdictionary; }
		internal set
		{
			if (value == null)
				return;

			this._xdictionary = value;
			this._xdictionary.Owner = this;

			if (this.Document != null)
				this.Document.RegisterCollection(this._xdictionary);
		}
	}

	private ExtendedDataDictionary _extendedData;

	// created on first AddReactor, most objects have none.
	private List<CadObject> _reactors;

	private CadDictionary _xdictionary = null;

	/// <summary>
	/// Default constructor.
	/// </summary>
	public CadObject()
	{
	}

	/// <summary>
	/// Add a reactor object linked to this one.
	/// </summary>
	/// <remarks>
	/// The <see cref="CadObject"/> and its reactors must be in the same <see cref="CadDocument"/> to be valid.
	/// </remarks>
	/// <param name="reactor"></param>
	public void AddReactor(CadObject reactor)
	{
		(this._reactors ??= new List<CadObject>()).Add(reactor);
	}

	/// <summary>
	/// Removes any reactor object that doesn't belong to the same <see cref="CadDocument"/> as this <see cref="CadObject"/>.
	/// </summary>
	public void CleanReactors()
	{
		if (this._reactors == null)
		{
			return;
		}

		var reactors = this._reactors.ToList();
		foreach (var reactor in reactors)
		{
			if (reactor.Document != this.Document)
			{
				this._reactors.Remove(reactor);
			}
		}
	}

	/// <summary>
	/// Creates a new object that is a copy of the current instance.
	/// </summary>
	/// <remarks>
	/// The copy will be unattached from the document or any reference.
	/// </remarks>
	/// <returns>A new object that is a copy of this instance.</returns>
	public virtual CadObject Clone()
	{
		CadObject clone = (CadObject)this.MemberwiseClone();

		clone.Handle = 0;

		clone.Document = null;
		clone.Owner = null;

		//Collections: MemberwiseClone copied the references of the source, whose owner is the
		//source object. The instances of the clone are created on first access.
		clone._reactors = null;
		clone._extendedData = null;
		clone.XDictionary = this._xdictionary?.CloneTyped();

		return clone;
	}

	/// <summary>
	/// Creates the extended dictionary if null.
	/// </summary>
	/// <returns>The <see cref="CadDictionary"/> attached to this <see cref="CadObject"/></returns>
	public CadDictionary CreateExtendedDictionary()
	{
		if (this._xdictionary == null)
		{
			this.XDictionary = new CadDictionary();
		}

		return this._xdictionary;
	}

	/// <summary>
	/// Remove a reactor linked to this object.
	/// </summary>
	/// <param name="reactor"></param>
	/// <returns></returns>
	public bool RemoveReactor(CadObject reactor)
	{
		return this._reactors != null && this._reactors.Remove(reactor);
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		return $"{this.ObjectName}:{this.Handle}";
	}

	internal virtual void AssignDocument(CadDocument doc)
	{
		this.Document = doc;

		if (this.XDictionary != null)
		{
			doc.RegisterCollection(this.XDictionary);
		}

		if (this._extendedData != null && this._extendedData.Any())
		{
			//Reset existing collection
			var entries = this._extendedData.ToArray();
			this._extendedData.Clear();

			foreach (var item in entries)
			{
				this._extendedData.Add(item.Key, item.Value);
			}
		}
	}

	internal virtual void UnassignDocument()
	{
		if (this.XDictionary != null)
		{
			this.Document.UnregisterCollection(this.XDictionary);
		}

		this.Handle = 0;
		this.Document = null;

		if (this._extendedData != null && this._extendedData.Any())
		{
			//Reset existing collection
			var entries = this._extendedData.ToArray();
			this._extendedData.Clear();

			foreach (var item in entries)
			{
				this._extendedData.Add(item.Key.Clone() as AppId, item.Value);
			}
		}

		this._reactors?.Clear();
	}

	protected static T updateCollection<T>(T entry, ICadCollection<T> table)
		where T : CadObject, INamedCadObject
	{
		if (table == null || entry == null)
		{
			return entry;
		}

		return table.TryAdd(entry);
	}
}