﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

[RequireComponent(typeof(Renderer))]
public class EpitaphRenderer : MonoBehaviour {
	public enum PropBlockType {
		Color,
		Float,
		Int,
		FloatArray
	}
	[Button("Print Property Block Value")]
	void PrintLookupValueCallback() {
		PrintPropBlockValue(lookupType, lookupString);
	}
	public bool printLookupValue = false;
	public PropBlockType lookupType = PropBlockType.Int;
	public string lookupString = "Look up anything";
	public const string mainColor = "_Color";

	Renderer lazy_r;
	public Renderer r {
		get {
			if (lazy_r == null) lazy_r = GetComponent<Renderer>();
			return lazy_r;
		}
	}

	MaterialPropertyBlock lazy_propBlock;
	MaterialPropertyBlock propBlock {
		get {
			if (lazy_propBlock == null) lazy_propBlock = new MaterialPropertyBlock();
			return lazy_propBlock;
		}
	}

	// Use this for initialization
	void Awake () {
		if (GetMaterial().HasProperty(mainColor)) {
			SetMainColor(r.material.color);
		}
	}

	public Color GetColor(string colorName) {
		r.GetPropertyBlock(propBlock);
		return propBlock.GetColor(colorName);
	}
	
	public Color GetMainColor() {
		return GetColor(mainColor);
	}

	public void SetColor(string colorName, Color color) {
		if (GetMaterial().HasProperty(colorName)) {
			r.GetPropertyBlock(propBlock);
			propBlock.SetColor(colorName, color);
			r.SetPropertyBlock(propBlock);
		}
	}

	public void SetMainColor(Color color) {
		SetColor(mainColor, color);
	}

	public Material GetMaterial() {
		return r.material;
	}

	public void SetMaterial(Material newMaterial, bool keepMainColor = true) {
		Color prevColor = GetMainColor();

		r.material = newMaterial;

		if (keepMainColor) {
			SetMainColor(prevColor);
		}
	}

	public Material[] GetMaterials() {
		return r.materials;
	}

	public void SetMaterials(Material[] newMaterials) {
		r.materials = newMaterials;
	}

	public void SetFloat(string propName, float value) {
		if (GetMaterial().HasProperty(propName)) {
			r.GetPropertyBlock(propBlock);
			propBlock.SetFloat(propName, value);
			r.SetPropertyBlock(propBlock);
		}
	}

	public float GetFloat(string propName) {
		r.GetPropertyBlock(propBlock);
		return propBlock.GetFloat(propName);
	}

	public void SetInt(string propName, int value) {
		if (GetMaterial().HasProperty(propName)) {
			r.GetPropertyBlock(propBlock);
			propBlock.SetInt(propName, value);
			r.SetPropertyBlock(propBlock);
		}
	}

	public int GetInt(string propName) {
		r.GetPropertyBlock(propBlock);
		return propBlock.GetInt(propName);
	}
	
	public void SetFloatArray(string propName, float[] value) {
		r.GetPropertyBlock(propBlock);
		propBlock.SetFloatArray(propName, value);
		r.SetPropertyBlock(propBlock);
	}
	
	public float[] GetFloatArray(string propName) {
		r.GetPropertyBlock(propBlock);
		return propBlock.GetFloatArray(propName);
	}

	public Bounds GetRendererBounds() {
		return r.bounds;
	}

	void PrintPropBlockValue(PropBlockType pbType, string key) {
		switch (pbType) {
			case PropBlockType.Color:
				Debug.Log(key + ": " + propBlock.GetColor(key));
				break;
			case PropBlockType.Float:
				Debug.Log(key + ": " + propBlock.GetFloat(key));
				break;
			case PropBlockType.Int:
				Debug.Log(key + ": " + propBlock.GetInt(key));
				break;
			case PropBlockType.FloatArray:
				Debug.Log(key + ": " + string.Join(", ", propBlock.GetFloatArray(key)));
				break;
		}
	}
}
