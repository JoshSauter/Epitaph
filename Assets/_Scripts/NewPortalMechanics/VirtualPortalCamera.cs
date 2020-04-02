﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EpitaphUtils;
using System;
using NaughtyAttributes;

public class VirtualPortalCamera : Singleton<VirtualPortalCamera> {
	public bool DEBUG = false;
	DebugLogger debug;
	Camera portalCamera;
	Camera mainCamera;

	public int MaxDepth = 6;
	public int MaxRenderSteps = 24;
	public float MaxRenderDistance = 400;
	public float distanceToStartCheckingPortalBounds = 12;
	public float clearSpaceBehindPortal = 0.49f;

	int renderSteps;
	public List<RenderTexture> renderStepTextures;

	[ShowIf("DEBUG")]
	public List<Portal> portalOrder = new List<Portal>();
	[ShowIf("DEBUG")]
	public List<RenderTexture> finishedTex = new List<RenderTexture>();

	private Rect fullScreenRect = new Rect(0, 0, 1, 1);

	void Start() {
		debug = new DebugLogger(gameObject, () => DEBUG);
		mainCamera = EpitaphScreen.instance.playerCamera;
		portalCamera = GetComponent<Camera>();
		EpitaphScreen.instance.OnPlayerCamPreRender += RenderPortals;
		EpitaphScreen.instance.OnScreenResolutionChanged += (width, height) => renderStepTextures.Clear();

		renderStepTextures = new List<RenderTexture>();
	}

	/// <summary>
	/// Will recursively render each portal surface visible in the scene before the Player's Camera draws the scene
	/// </summary>
	void RenderPortals() {
		List<Portal> allActivePortals = NewPortalManager.instance.activePortals;
		Dictionary<Portal, RenderTexture> finishedPortalTextures = new Dictionary<Portal, RenderTexture>();

		Vector3 camPosition = mainCamera.transform.position;
		Quaternion camRotation = mainCamera.transform.rotation;
		Matrix4x4 camProjectionMatrix = mainCamera.projectionMatrix;
		SetCameraSettings(portalCamera, camPosition, camRotation, camProjectionMatrix);

		if (DEBUG) {
			portalOrder.Clear();
			finishedTex.Clear();
		}

		renderSteps = 0;
		foreach (var p in allActivePortals) {
			float distanceFromPortalToCam = Vector3.Distance(mainCamera.transform.position, p.ClosestPoint(mainCamera.transform.position));
			Rect portalScreenBounds = (distanceFromPortalToCam > distanceToStartCheckingPortalBounds) ? p.GetScreenRect(mainCamera) : fullScreenRect;

			// Always render a portal when its volumetric portal is enabled (PortalIsSeenByCamera may be false when the player is in the portal)
			if (PortalIsSeenByCamera(p, mainCamera, fullScreenRect, portalScreenBounds) || p.IsVolumetricPortalEnabled()) {
				SetCameraSettings(portalCamera, camPosition, camRotation, camProjectionMatrix);

				finishedPortalTextures[p] = RenderPortalDepth(0, p, portalScreenBounds, p.name);

				if (DEBUG) {
					portalOrder.Add(p);
					finishedTex.Add(finishedPortalTextures[p]);
				}
			}
		}

		foreach (var finishedPortalTexture in finishedPortalTextures) {
			finishedPortalTexture.Key.SetTexture(finishedPortalTexture.Value);
		}
		debug.LogWarning("End of frame: renderSteps: " + renderSteps);
	}

	RenderTexture RenderPortalDepth(int depth, Portal portal, Rect portalScreenBounds, string tree) {
		if (depth == MaxDepth || renderSteps >= MaxRenderSteps) return null;

		var index = renderSteps;
		renderSteps++;

		SetupPortalCameraForPortal(portal, portal.otherPortal, depth);

		Vector3 modifiedCamPosition = portalCamera.transform.position;
		Quaternion modifiedCamRotation = portalCamera.transform.rotation;
		Matrix4x4 modifiedCamProjectionMatrix = portalCamera.projectionMatrix;

		// Key == Visible Portal, Value == visible portal screen bounds
		Dictionary<Portal, Rect> visiblePortals = GetVisiblePortalsAndTheirScreenBounds(portal, portalScreenBounds);

		debug.Log("Depth (Index): " + depth + " (" + index + ")\nPortal: " + portal.name + "\nNumVisible: " + visiblePortals.Count + "\nPortalCamPos: " + portalCamera.transform.position + "\nTree: " + tree + "\nScreenBounds: " + portalScreenBounds);

		Dictionary<Portal, RenderTexture> visiblePortalTextures = new Dictionary<Portal, RenderTexture>();
		foreach (var visiblePortalTuple in visiblePortals) {
			Portal visiblePortal = visiblePortalTuple.Key;

			bool isWithinRenderDistance = Vector3.Distance(visiblePortal.transform.position, portalCamera.transform.position) < MaxRenderDistance;

			if (depth < MaxDepth - 1 && isWithinRenderDistance) {
				string nextTree = tree + ", " + visiblePortal.name;
				Rect visiblePortalRect = visiblePortalTuple.Value;
				Rect nextPortalBounds = IntersectionOfBounds(portalScreenBounds, visiblePortalRect);

				// Remember state
				visiblePortalTextures[visiblePortal] = RenderPortalDepth(depth + 1, visiblePortal, nextPortalBounds, nextTree);
			}
			else {
				visiblePortal.DefaultMaterial();
			}

			// RESTORE STATE
			SetCameraSettings(portalCamera, modifiedCamPosition, modifiedCamRotation, modifiedCamProjectionMatrix);
		}

		// RESTORE STATE
		foreach (var visiblePortalKeyVal in visiblePortals) {
			Portal visiblePortal = visiblePortalKeyVal.Key;
			bool isWithinRenderDistance = Vector3.Distance(visiblePortal.transform.position, portalCamera.transform.position) < MaxRenderDistance;

			if (depth < MaxDepth - 1 && isWithinRenderDistance) {
				// Restore the RenderTextures that were in use at this stage
				visiblePortal.SetTexture(visiblePortalTextures[visiblePortalKeyVal.Key]);
			}
			else {
				visiblePortal.DefaultMaterial();
			}
		}
		SetCameraSettings(portalCamera, modifiedCamPosition, modifiedCamRotation, modifiedCamProjectionMatrix);

		while (renderStepTextures.Count <= index) {
			renderStepTextures.Add(new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32));
		}

		debug.Log("Rendering: " + index + " to " + portal.name + "'s RenderTexture, depth: " + depth);
		portalCamera.targetTexture = renderStepTextures[index];
		portalCamera.Render();

		portal.SetTexture(renderStepTextures[index]);
		return renderStepTextures[index];
	}

	/// <summary>
	/// Finds all visible portals from this portal and stores them in a Dictionary with their screen bounds
	/// </summary>
	/// <param name="portal">The "in" portal</param>
	/// <param name="portalScreenBounds">The screen bounds of the "in" portal, [0-1]</param>
	/// <returns>A dictionary where each key is a visible portal and each value is the screen bounds of that portal</returns>
	Dictionary<Portal, Rect> GetVisiblePortalsAndTheirScreenBounds(Portal portal, Rect portalScreenBounds) {
		Dictionary<Portal, Rect> visiblePortals = new Dictionary<Portal, Rect>();
		foreach (var p in NewPortalManager.instance.activePortals) {
			// Ignore the portal we're looking through
			if (p == portal.otherPortal) continue;

			Rect testPortalBounds = p.GetScreenRect(portalCamera);
			if (PortalIsSeenByCamera(p, portalCamera, portalScreenBounds, testPortalBounds)) {
				visiblePortals.Add(p, testPortalBounds);
			}
		}

		return visiblePortals;
	}

	bool PortalIsSeenByCamera(Portal testPortal, Camera cam, Rect portalScreenBounds, Rect testPortalBounds) {
		bool isInCameraFrustum = testPortal.IsVisibleFrom(cam);
		bool isWithinParentPortalScreenBounds = portalScreenBounds.Overlaps(testPortalBounds);
		bool isFacingCamera = Vector3.Dot(testPortal.transform.forward, (cam.transform.position - testPortal.transform.position).normalized) < 0.05f;
		return isInCameraFrustum && isWithinParentPortalScreenBounds && isFacingCamera;
	}

	void SetCameraSettings(Camera cam, Vector3 position, Quaternion rotation, Matrix4x4 projectionMatrix) {
		cam.transform.position = position;
		cam.transform.rotation = rotation;
		cam.projectionMatrix = projectionMatrix;
	}

	Rect IntersectionOfBounds(Rect a, Rect b) {
		Rect intersection = new Rect();
		intersection.min = Vector2.Max(a.min, b.min);
		intersection.max = Vector2.Min(a.max, b.max);
		return intersection;
	}

	void SetupPortalCameraForPortal(Portal inPortal, Portal outPortal, int depth) {
		Transform inTransform = inPortal.transform;
		Transform outTransform = outPortal.transform;
		// Position the camera behind the other portal.
		Vector3 relativePos = inTransform.InverseTransformPoint(portalCamera.transform.position);
		relativePos = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativePos;
		portalCamera.transform.position = outTransform.TransformPoint(relativePos);

		// Rotate the camera to look through the other portal.
		Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * portalCamera.transform.rotation;
		relativeRot = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativeRot;
		portalCamera.transform.rotation = outTransform.rotation * relativeRot;

		// Set the camera's oblique view frustum.
		bool shouldUseDefaultProjectionMatrix = depth == 0 && Vector3.Distance(mainCamera.transform.position, inPortal.ClosestPoint(mainCamera.transform.position)) < clearSpaceBehindPortal;
		if (!shouldUseDefaultProjectionMatrix) {
			Plane p = new Plane(-outTransform.forward, outTransform.position + clearSpaceBehindPortal * outTransform.forward);
			Vector4 clipPlane = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
			Vector4 clipPlaneCameraSpace = Matrix4x4.Transpose(Matrix4x4.Inverse(portalCamera.worldToCameraMatrix)) * clipPlane;

			var newMatrix = mainCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
			//Debug.Log("Setting custom matrix: " + )
			portalCamera.projectionMatrix = newMatrix;
		}
		else {
			portalCamera.projectionMatrix = mainCamera.projectionMatrix;
		}

	}

}
