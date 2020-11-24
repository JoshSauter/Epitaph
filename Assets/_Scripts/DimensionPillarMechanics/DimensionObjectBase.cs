﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EpitaphUtils;
using EpitaphUtils.ShaderUtils;
using System.Linq;
using Saving;
using System;

public enum VisibilityState {
	invisible,
	partiallyVisible,
	visible,
	partiallyInvisible,
};

[RequireComponent(typeof(UniqueId))]
public class DimensionObjectBase : MonoBehaviour, SaveableObject {
	UniqueId _id;
	public UniqueId id {
		get {
			if (_id == null) {
				_id = GetComponent<UniqueId>();
			}
			return _id;
		}
	}

	public bool DEBUG = false;
	public bool treatChildrenAsOneObjectRecursively = false;
	protected DebugLogger debug;

	protected bool initialized = false;
	[Range(0, 1)]
	public int channel;
	[Range(0, 7)]
	public int baseDimension = 1;
	public bool reverseVisibilityStates = false;
	public bool ignoreMaterialChanges = false;
	protected int curDimensionSetInMaterial;

	public EpitaphRenderer[] renderers;
	public Dictionary<EpitaphRenderer, Material[]> startingMaterials;
	public Dictionary<EpitaphRenderer, int> startingLayers;

	public VisibilityState startingVisibilityState = VisibilityState.visible;
	public VisibilityState visibilityState = VisibilityState.visible;
	protected static Dictionary<VisibilityState, HashSet<VisibilityState>> nextStates = new Dictionary<VisibilityState, HashSet<VisibilityState>> {
		{ VisibilityState.invisible, new HashSet<VisibilityState> { VisibilityState.partiallyVisible, VisibilityState.partiallyInvisible } },
		{ VisibilityState.partiallyVisible, new HashSet<VisibilityState> { VisibilityState.invisible, VisibilityState.visible } },
		{ VisibilityState.visible, new HashSet<VisibilityState> { VisibilityState.partiallyVisible, VisibilityState.partiallyInvisible } },
		{ VisibilityState.partiallyInvisible, new HashSet<VisibilityState> { VisibilityState.invisible, VisibilityState.visible } }
	};

	#region events
	public delegate void DimensionObjectAction();
	public event DimensionObjectAction OnBaseDimensionChange;
	public delegate void DimensionObjectStateChangeAction(VisibilityState visibilityState);
	public event DimensionObjectStateChangeAction OnStateChange;
	#endregion

	private void Awake() {
		debug = new DebugLogger(this, () => DEBUG);

		renderers = GetAllEpitaphRenderers().ToArray();
		if (renderers.Length == 0) {
			Debug.LogError("No renderers found for: " + gameObject.name, gameObject);
			enabled = false;
		}
		startingMaterials = GetAllStartingMaterials(renderers);
		startingLayers = GetAllStartingLayers(renderers);
	}

	public virtual IEnumerator Start() {
		SetChannelValuesInMaterials();

		if (!initialized) {
			yield return new WaitUntil(() => gameObject.IsInLoadedScene());
			SwitchVisibilityState(startingVisibilityState, true);
			initialized = true;
		}
	}

	private void OnEnable() {
		LevelManager.instance.OnActiveSceneChange += OnActiveSceneChange;
	}

	private void OnDisable() {
		LevelManager.instance.OnActiveSceneChange -= OnActiveSceneChange;
	}

	void OnActiveSceneChange() {
		if (gameObject.IsInActiveScene()) {
			SwitchVisibilityState(startingVisibilityState, true);
		}
	}

	public void OverrideStartingMaterials(Dictionary<EpitaphRenderer, Material[]> newStartingMaterials) {
		startingMaterials = newStartingMaterials;
	}

	public virtual void SetBaseDimension(int newBaseDimension) {
		if (newBaseDimension != baseDimension && OnBaseDimensionChange != null) {
			OnBaseDimensionChange();
		}
		baseDimension = newBaseDimension;
	}

	////////////////////////
	// State Change Logic //
	////////////////////////
	#region stateChange

	public virtual void SwitchVisibilityState(VisibilityState nextState, bool ignoreTransitionRules = false) {
		if (gameObject.activeInHierarchy) {
			StartCoroutine(SwitchVisibilityStateCoroutine(nextState, ignoreTransitionRules));
		}
	}

	public virtual IEnumerator SwitchVisibilityStateCoroutine(VisibilityState nextState, bool ignoreTransitionRules = false) {
		if (!(ignoreTransitionRules || IsValidNextState(nextState))) yield break;

		debug.Log("State transition: " + visibilityState + " --> " + nextState);

		int setDimension = -1;
		switch (nextState) {
			case VisibilityState.invisible:
				visibilityState = VisibilityState.invisible;
				break;
			case VisibilityState.partiallyVisible:
				visibilityState = VisibilityState.partiallyVisible;
				setDimension = (DimensionPillar.activePillar != null) ? DimensionPillar.activePillar.curDimension : baseDimension;
				break;
			case VisibilityState.visible:
				visibilityState = VisibilityState.visible;
				break;
			case VisibilityState.partiallyInvisible:
				visibilityState = VisibilityState.partiallyInvisible;
				setDimension = (DimensionPillar.activePillar != null) ? DimensionPillar.activePillar.curDimension : baseDimension;
				if (reverseVisibilityStates) setDimension = DimensionPillar.activePillar?.NextDimension(setDimension) ?? setDimension;
				break;
		}

		if (!ignoreMaterialChanges) {
			foreach (var r in renderers) {
				SetMaterials(r);
			}
			if (setDimension >= 0) {
				SetDimensionValuesInMaterials(setDimension);
			}
			SetChannelValuesInMaterials();
		}

		// Give a frame to let new materials switch before calling state change event
		yield return null;
		OnStateChange?.Invoke(nextState);
	}

	private bool IsValidNextState(VisibilityState nextState) {
		return nextStates[visibilityState].Contains(nextState);
	}
	#endregion

	///////////////////////////
	// Material Change Logic //
	///////////////////////////
	#region materials
	void SetMaterials(EpitaphRenderer renderer) {
		Material[] normalMaterials = startingMaterials[renderer];
		Material[] newMaterials;
		bool inverseShader = false;
		if (!reverseVisibilityStates) {
			if (visibilityState == VisibilityState.partiallyVisible) {
				newMaterials = normalMaterials.Select(m => GetDimensionObjectMaterial(m)).ToArray();
			}
			else if (visibilityState == VisibilityState.partiallyInvisible) {
				newMaterials = normalMaterials.Select(m => GetDimensionObjectMaterial(m)).ToArray();
				inverseShader = true;
			}
			else {
				newMaterials = normalMaterials;
			}
		}
		else {
			if (visibilityState == VisibilityState.partiallyVisible) {
				newMaterials = normalMaterials.Select(m => GetDimensionObjectMaterial(m)).ToArray();
				inverseShader = true;
			}
			else if (visibilityState == VisibilityState.partiallyInvisible) {
				newMaterials = normalMaterials.Select(m => GetDimensionObjectMaterial(m)).ToArray();
			}
			else {
				newMaterials = normalMaterials;
			}
		}

		bool invisibleLayer = visibilityState == VisibilityState.invisible;
		if (reverseVisibilityStates) invisibleLayer = visibilityState == VisibilityState.visible;
		renderer.gameObject.layer = invisibleLayer ? LayerMask.NameToLayer("Invisible") : startingLayers[renderer];

		renderer.SetMaterials(newMaterials);
		renderer.SetInt("_Inverse", inverseShader ? 1 : 0);
	}

	protected void SetDimensionValuesInMaterials(int newDimensionValue) {
		bool isDimensionShader = (visibilityState == VisibilityState.partiallyVisible || visibilityState == VisibilityState.partiallyInvisible);
		if (curDimensionSetInMaterial != newDimensionValue && isDimensionShader && IsRelevantDimension(newDimensionValue)) {
			foreach (var r in renderers) {
				r.SetInt("_Dimension", newDimensionValue);
			}
			curDimensionSetInMaterial = newDimensionValue;
		}
	}

	protected virtual bool IsRelevantDimension(int dimension) {
		return dimension == baseDimension || (reverseVisibilityStates && visibilityState == VisibilityState.partiallyInvisible && dimension == baseDimension+1);
	}

	protected void SetChannelValuesInMaterials() {
		foreach (var r in renderers) {
			r.SetInt("_Channel", channel);
		}
	}

	protected List<EpitaphRenderer> GetAllEpitaphRenderers() {
		List<EpitaphRenderer> allRenderers = new List<EpitaphRenderer>();
		if (!treatChildrenAsOneObjectRecursively) {
			EpitaphRenderer thisRenderer = GetComponent<EpitaphRenderer>();
			if (thisRenderer == null && GetComponent<Renderer>() != null) {
				thisRenderer = gameObject.AddComponent<EpitaphRenderer>();
			}
			if (thisRenderer != null) {
				allRenderers.Add(thisRenderer);
			}
		}
		else {
			SetEpitaphRenderersRecursively(transform, ref allRenderers);
		}
		return allRenderers;
	}

	void SetEpitaphRenderersRecursively(UnityEngine.Transform parent, ref List<EpitaphRenderer> renderersSoFar) {
		// Children who have DimensionObject scripts are treated on only by their own settings
		if (parent != transform && parent.GetComponent<PillarDimensionObject>() != null) return;

		EpitaphRenderer thisRenderer = parent.GetComponent<EpitaphRenderer>();
		if (thisRenderer == null && parent.GetComponent<Renderer>() != null) {
			thisRenderer = parent.gameObject.AddComponent<EpitaphRenderer>();
		}

		if (thisRenderer != null) {
			renderersSoFar.Add(thisRenderer);
		}

		if (parent.childCount > 0) {
			foreach (UnityEngine.Transform child in parent) {
				SetEpitaphRenderersRecursively(child, ref renderersSoFar);
			}
		}
	}

	protected Dictionary<EpitaphRenderer, Material[]> GetAllStartingMaterials(EpitaphRenderer[] renderers) {
		Dictionary<EpitaphRenderer, Material[]> dict = new Dictionary<EpitaphRenderer, Material[]>();
		foreach (var r in renderers) {
			dict.Add(r, r.GetMaterials());
		}

		return dict;
	}

	protected Dictionary<EpitaphRenderer, int> GetAllStartingLayers(EpitaphRenderer[] renderers) {
		Dictionary<EpitaphRenderer, int> dict = new Dictionary<EpitaphRenderer, int>();
		foreach (var r in renderers) {
			dict.Add(r, r.gameObject.layer);
		}

		return dict;
	}

	private Material GetDimensionObjectMaterial(Material normalMaterial) {
		Material newMaterial = null;
		switch (normalMaterial.shader.name) {
			case "Custom/Unlit":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/DimensionObject"));
				break;
			case "Custom/UnlitDissolve":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/DimensionDissolve"));
				break;
			case "Standard (Specular setup)":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/DimensionObjectSpecular"));
				break;
			case "TextMeshPro/Mobile/Distance Field":
				if (normalMaterial.name.Contains("LiberationSans SDF")) {
					newMaterial = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - InGameTextDimensionObject");
				}
				else {
					Debug.LogWarning("No DimensionObject font material for " + normalMaterial.name);
				}
				break;
			case "Hidden/Raymarching":
			case "Hidden/RaymarchingDissolve":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/DimensionRaymarching"));
				break;
			case "Custom/InvertColorsObject":
			case "Custom/InvertColorsObjectDissolve":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/DimensionInvertColorsObject"));
				break;
			case "Custom/Water":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/DimensionWater"));
				break;
			default:
				debug.LogWarning("No matching dimensionObjectShader for shader " + normalMaterial.shader.name);
				break;
		}

		if (newMaterial != null && normalMaterial != null) {
			newMaterial.CopyMatchingPropertiesFromMaterial(normalMaterial);
		}
		return (newMaterial != null) ? newMaterial : normalMaterial;
	}
	#endregion

	#region Saving
	public bool SkipSave { get; set; }
	// There's only one player so we don't need a UniqueId here
	public virtual string ID => $"DimensionObjectBase_{id.uniqueId}";
	//public virtual string ID {
	//	get {
	//		if (id == null || id.uniqueId == null) {
	//			throw new Exception($"{gameObject.name} in {gameObject.scene.name} doesn't have a uniqueId set");
	//		}
	//		return $"DimensionObjectBase_{id.uniqueId}";
	//	}
	//}

	[Serializable]
	class DimensionObjectBaseSave {
		bool treatChildrenAsOneObjectRecursively;

		bool initialized;
		int channel;
		int baseDimension;
		bool reverseVisibilityStates;
		bool ignoreMaterialChanges;
		int curDimensionSetInMaterial;

		int startingVisibilityState;
		int visibilityState;

		public DimensionObjectBaseSave(DimensionObjectBase dimensionObj) {
			this.treatChildrenAsOneObjectRecursively = dimensionObj.treatChildrenAsOneObjectRecursively;
			this.initialized = dimensionObj.initialized;
			this.channel = dimensionObj.channel;
			this.baseDimension = dimensionObj.baseDimension;
			this.reverseVisibilityStates = dimensionObj.reverseVisibilityStates;
			this.ignoreMaterialChanges = dimensionObj.ignoreMaterialChanges;
			this.curDimensionSetInMaterial = dimensionObj.curDimensionSetInMaterial;
			this.startingVisibilityState = (int)dimensionObj.startingVisibilityState;
			this.visibilityState = (int)dimensionObj.visibilityState;
		}

		public void LoadSave(DimensionObjectBase dimensionObj) {
			dimensionObj.treatChildrenAsOneObjectRecursively = this.treatChildrenAsOneObjectRecursively;
			dimensionObj.initialized = this.initialized;
			dimensionObj.channel = this.channel;
			dimensionObj.baseDimension = this.baseDimension;
			dimensionObj.reverseVisibilityStates = this.reverseVisibilityStates;
			dimensionObj.ignoreMaterialChanges = this.ignoreMaterialChanges;
			dimensionObj.curDimensionSetInMaterial = this.curDimensionSetInMaterial;
			dimensionObj.startingVisibilityState = (VisibilityState)this.startingVisibilityState;
			dimensionObj.visibilityState = (VisibilityState)this.visibilityState;

			dimensionObj.SwitchVisibilityState(dimensionObj.visibilityState, true);
		}
	}

	public virtual object GetSaveObject() {
		return new DimensionObjectBaseSave(this);
	}

	public virtual void LoadFromSavedObject(object savedObject) {
		DimensionObjectBaseSave save = savedObject as DimensionObjectBaseSave;

		save.LoadSave(this);
	}
	#endregion
}
