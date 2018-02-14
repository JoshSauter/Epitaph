﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EpitaphUtils;

public class SketchOverlay : MonoBehaviour {
	public float timeBetweenShifts = 0.1f;
	public float intensity = 0.3f;
	// Minimum and maximum X and Y coordinates to sample the sketch texture from
	Vector2 subSampleMin = new Vector2(0.25f, 0.25f);
	Vector2 subSampleMax = new Vector2(0.75f, 0.75f);

	Vector2 offset = new Vector2();
	Vector2 scale = new Vector2();

	Material mat;

	// Use this for initialization
	void Awake () {
		mat = Resources.Load<Material>("Materials/Overlay/SketchEffectMaterial");
	}

	IEnumerator Start() {
		while (enabled) {
			SetRandomOffsetScale();
			yield return new WaitForSeconds(timeBetweenShifts);
		}
	}

	private void OnRenderImage(RenderTexture source, RenderTexture destination) {
		mat.SetFloat("_Intensity", intensity);
		mat.SetTextureOffset("_SketchTex", offset);
		mat.SetTextureScale("_SketchTex", scale);
		Graphics.Blit(source, destination, mat);
	}

	/// <summary>
	/// Sets a random texture offset and scale between subSampleMin and subSampleMax.
	/// </summary>
	private void SetRandomOffsetScale() {
		scale = new Vector2(0.4f, 0.4f);
		Vector2 minOffset = subSampleMin;
		Vector2 maxOffset = subSampleMax - scale;
		offset = Vector2.Scale((Random.insideUnitCircle), (maxOffset - minOffset)) + minOffset;
	}
}
