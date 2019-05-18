﻿using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class CameraDebugOverlay : MonoBehaviour {
	[SerializeField]
	Material mat;

	KeyCode modeSwitchKey = KeyCode.N;

	public enum DebugMode {
		depth,
		normals,
		obliqueness,
		off
	}
	public DebugMode debugMode = DebugMode.off;

	private const int NUM_MODES = 4;
	int mode {
		get {
			return (int)debugMode;
		}
	}

	void Start() {
		GetComponent<Camera>().depthTextureMode = DepthTextureMode.DepthNormals;
	}

	private void Update() {
		if (Input.GetKeyDown(modeSwitchKey)) {
			debugMode = (DebugMode)(((int)debugMode + 1) % NUM_MODES);
		}
	}

	void OnRenderImage(RenderTexture source, RenderTexture destination) {
		if (mode < NUM_MODES - 1) {
			Graphics.Blit(source, destination, mat, mode);
		}
		else {
			Graphics.Blit(source, destination);
		}
	}
}