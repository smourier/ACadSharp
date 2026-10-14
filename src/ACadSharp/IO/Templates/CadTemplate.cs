using ACadSharp.Classes;
using ACadSharp.Entities;
using ACadSharp.Objects;
using ACadSharp.Tables;
using ACadSharp.XData;
using System.Collections.Generic;

namespace ACadSharp.IO.Templates;

internal abstract class CadTemplate : ICadObjectTemplate
{
	public CadObject CadObject { get; set; }

	public DxfClass DxfClass { get; set; }

	// created on first access, most objects carry no extended data and no reactors.
	public Dictionary<ulong, List<ExtendedDataRecord>> EDataTemplate
	{
		get { return this._eDataTemplate ??= new(); }
		set { this._eDataTemplate = value; }
	}

	public Dictionary<string, List<ExtendedDataRecord>> EDataTemplateByAppName
	{
		get { return this._eDataTemplateByAppName ??= new(); }
		set { this._eDataTemplateByAppName = value; }
	}

	public bool HasBeenBuilt { get; private set; } = false;

	public bool HasDsBinaryData { get; set; }

	public ulong? OwnerHandle { get; set; }

	public HashSet<ulong> ReactorsHandles
	{
		get { return this._reactorsHandles ??= new(); }
		set { this._reactorsHandles = value; }
	}

	private Dictionary<ulong, List<ExtendedDataRecord>> _eDataTemplate;

	private Dictionary<string, List<ExtendedDataRecord>> _eDataTemplateByAppName;

	private HashSet<ulong> _reactorsHandles;

	public ulong? XDictHandle { get; set; }

	public CadTemplate(CadObject cadObject)
	{
		this.CadObject = cadObject;
	}

	public void Build(CadDocumentBuilder builder)
	{
		if (this.HasBeenBuilt)
		{
			return;
		}
		else
		{
			this.HasBeenBuilt = true;
		}

		this.build(builder);

		if (this.DxfClass != null && this.CadObject is not IDxfClassDefined)
		{
			switch (this.CadObject)
			{
				case ProxyEntity:
				case UnknownEntity:
				case UnknownNonGraphicalObject:
					break;
				default:
					builder.Notify($"{this.CadObject.GetType().FullName} does not implement {nameof(IDxfClassDefined)}", NotificationType.Warning);
					break;
			}
		}

		builder.NotifyProgress(ReadStage.Build, this);
	}

	public virtual CadObjectData GetObjectData()
	{
		return new CadObjectData(this.CadObject, this.OwnerHandle, this.XDictHandle);
	}

	public override string ToString()
	{
		return $"{this.CadObject?.ToString()}";
	}

	protected virtual void build(CadDocumentBuilder builder)
	{
		if (builder.TryGetCadObject(this.XDictHandle, out CadDictionary cadDictionary))
		{
			this.CadObject.XDictionary = cadDictionary;
		}

		if (this._reactorsHandles != null)
		{
			foreach (ulong handle in this._reactorsHandles)
			{
				if (builder.TryGetCadObject(handle, out CadObject reactor))
				{
					this.CadObject.AddReactor(reactor);
				}
				else
				{
					builder.Notify($"Reactor with handle {handle} not found", NotificationType.Warning);
				}
			}
		}

		if (this._eDataTemplate != null)
		{
			foreach (var item in this._eDataTemplate)
			{
				if (builder.TryGetCadObject(item.Key, out AppId app))
				{
					this.CadObject.ExtendedData.Add(app, item.Value);
				}
				else
				{
					builder.Notify($"AppId in extended data with handle {item.Key} not found", NotificationType.Warning);
				}
			}
		}

		if (this._eDataTemplateByAppName != null)
		{
			foreach (var item in this._eDataTemplateByAppName)
			{
				if (builder.TryGetTableEntry(item.Key, out AppId app))
				{
					this.CadObject.ExtendedData.Add(app, item.Value);
				}
				else
				{
					builder.Notify($"AppId in extended data with handle {item.Key} not found", NotificationType.Warning);
				}
			}
		}
	}

	protected IEnumerable<T> getEntitiesCollection<T>(CadDocumentBuilder builder, ulong firstHandle, ulong endHandle)
		where T : Entity
	{
		CadEntityTemplate template = builder.GetObjectTemplate<CadEntityTemplate>(firstHandle);

		if (template == null)
		{
			builder.Notify($"Leading entity with handle {firstHandle} not found.", NotificationType.Warning);
			template = builder.GetObjectTemplate<CadEntityTemplate>(endHandle);
		}

		while (template != null)
		{
			yield return (T)template.CadObject;

			if (template.CadObject.Handle == endHandle)
			{
				break;
			}

			if (template.NextEntity.HasValue)
			{
				template = builder.GetObjectTemplate<CadEntityTemplate>(template.NextEntity.Value);
			}
			else
			{
				template = builder.GetObjectTemplate<CadEntityTemplate>(template.CadObject.Handle + 1);
			}
		}
	}

	protected bool getTableReference<T>(CadDocumentBuilder builder, ulong? handle, string name, out T reference)
		where T : TableEntry
	{
		if (builder.TryGetCadObject<T>(handle, out reference) || builder.TryGetTableEntry<T>(name, out reference))
		{
			return true;
		}
		else
		{
			if (!string.IsNullOrEmpty(name) || (handle.HasValue && handle.Value != 0))
			{
				builder.Notify($"{typeof(T).FullName} table reference with handle: {handle} | name: {name} not found for {this.CadObject.GetType().FullName} with handle {this.CadObject.Handle}", NotificationType.Warning);
			}

			return false;
		}
	}
}