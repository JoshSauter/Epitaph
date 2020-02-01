﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EpitaphUtils;
using EpitaphUtils.ShaderUtils;
using System.Linq;

public enum VisibilityState {
	invisible,
	partiallyVisible,
	visible,
	partiallyInvisible,
};

public class DimensionObjectBase : MonoBehaviour {
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

	protected virtual void Start() {
		Init();
	}

	public void OverrideStartingMaterials(Dictionary<EpitaphRenderer, Material[]> newStartingMaterials) {
		startingMaterials = newStartingMaterials;
	}

	public virtual void Init() {
		if (!initialized) {
			debug = new DebugLogger(gameObject, DEBUG);

			renderers = GetAllEpitaphRenderers().ToArray();
			startingMaterials = GetAllStartingMaterials(renderers);
			SetChannelValuesInMaterials();

			SwitchVisibilityState(startingVisibilityState, true);

			initialized = true;
		}
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
		if (!ignoreTransitionRules && !IsValidNextState(nextState)) return;

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
				break;
		}

		if (!ignoreMaterialChanges) {
			foreach (var r in renderers) {
				SetMaterials(r);

			}
			if (setDimension > 0) {
				SetDimensionValuesInMaterials(setDimension);
			}
			SetChannelValuesInMaterials();
		}

		if (OnStateChange != null) {
			OnStateChange(nextState);
		}
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
		if (!reverseVisibilityStates) {
			if (visibilityState == VisibilityState.partiallyVisible) {
				newMaterials = normalMaterials.Select(m => GetDimensionObjectMaterial(m)).ToArray();
			}
			else if (visibilityState == VisibilityState.partiallyInvisible) {
				newMaterials = normalMaterials.Select(m => GetInverseDimensionObjectMaterial(m)).ToArray();
			}
			else {
				newMaterials = normalMaterials;
			}
		}
		else {
			if (visibilityState == VisibilityState.partiallyVisible) {
				newMaterials = normalMaterials.Select(m => GetInverseDimensionObjectMaterial(m)).ToArray();
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
		renderer.gameObject.layer = LayerMask.NameToLayer(invisibleLayer ? "Invisible" : "Default");

		renderer.SetMaterials(newMaterials);
	}

	protected void SetDimensionValuesInMaterials(int newDimensionValue) {
		if (curDimensionSetInMaterial != newDimensionValue && (visibilityState == VisibilityState.partiallyVisible || visibilityState == VisibilityState.partiallyInvisible)) {
			foreach (var r in renderers) {
				r.SetInt("_Dimension", newDimensionValue);
			}
			curDimensionSetInMaterial = newDimensionValue;
		}
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

	void SetEpitaphRenderersRecursively(Transform parent, ref List<EpitaphRenderer> renderersSoFar) {
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
			foreach (Transform child in parent) {
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

	private Material GetDimensionObjectMaterial(Material normalMaterial) {
		Material newMaterial = null;
		switch (normalMaterial.shader.name) {
			case "Custom/Unlit":
			case "Custom/UnlitDissolve":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/DimensionObject"));
				break;
			case "Standard (Specular setup)":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/DimensionObjectSpecular"));
				break;
			case "TextMeshPro/Distance Field":
				if (normalMaterial.name.Contains("Signika-Regular SDF Material")) {
					newMaterial = Resources.Load<Material>("Fonts/Signika-Regular SDF DimensionObject");
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
			default:
				Debug.LogWarning("No matching dimensionObjectShader for shader " + normalMaterial.shader.name);
				break;
		}

		if (newMaterial != null && normalMaterial != null) {
			newMaterial.CopyMatchingPropertiesFromMaterial(normalMaterial);
		}
		// ?? means: return (newMaterial != null) ? newMaterial : normalMaterial;
		return newMaterial ?? normalMaterial;
	}

	private Material GetInverseDimensionObjectMaterial(Material normalMaterial) {
		Material newMaterial = null;
		switch (normalMaterial.shader.name) {
			case "Custom/Unlit":
			case "Custom/UnlitDissolve":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/InverseDimensionObject"));
				break;
			case "Standard (Specular setup)":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/InverseDimensionObjectSpecular"));
				break;
			case "TextMeshPro/Distance Field":
				if (normalMaterial.name.Contains("Signika-Regular SDF Material")) {
					newMaterial = Resources.Load<Material>("Fonts/Signika-Regular SDF InverseDimensionObject");
				}
				else {
					Debug.LogWarning("No DimensionObject font material for " + normalMaterial.name);
				}
				break;
			case "Hidden/Raymarching":
			case "Hidden/RaymarchingDissolve":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/InverseDimensionRaymarching"));
				break;
			case "Custom/InvertColorsObject":
			case "Custom/InvertColorsObjectDissolve":
				newMaterial = new Material(Shader.Find("Custom/DimensionShaders/InverseDimensionInvertColorsObject"));
				break;
			default:
				Debug.LogWarning("No matching dimensionObjectShader for shader " + normalMaterial.shader.name);
				break;
		}

		if (newMaterial != null && normalMaterial != null) {
			newMaterial.CopyMatchingPropertiesFromMaterial(normalMaterial);
		}
		// ?? means: return (newMaterial != null) ? newMaterial : normalMaterial;
		return newMaterial ?? normalMaterial;
	}
	#endregion
}
