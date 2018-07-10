﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour {
	// Only things set in Inspector, everything else should be automatic
	public Transform otherPortal;
	public Color portalEdgeDetectionColor = Color.black;
	public bool teleportOnEnter = true;
	public float portalOffset = 0;
	public float colliderOffset = 0;
	public bool useMeshAsCollider = false;

	// Transforms
	[HideInInspector]
	public Transform[] portals = new Transform[2];
	public Transform portalA {
		get { return portals[0]; }
		set { portals[0] = value; }
	}
	public Transform portalB {
		get { return portals[1]; }
		set { portals[1] = value; }
	}

	// Trigger Colliders
	[HideInInspector]
	public Collider[] triggerColliders = new Collider[2];
	public Collider triggerColliderA {
		get { return triggerColliders[0]; }
		set { triggerColliders[0] = value; }
	}
	public Collider triggerColliderB {
		get { return triggerColliders[1]; }
		set { triggerColliders[1] = value; }
	}

	// Normals
	[HideInInspector]
	public Vector3[] portalForwards = new Vector3[2];
	public Vector3 portalForwardA {
		get { return portalForwards[0]; }
		set { portalForwards[0] = value; }
	}
	public Vector3 portalForwardB {
		get { return portalForwards[1]; }
		set { portalForwards[1] = value; }
	}

	// Render Textures / Camera Follow
	[HideInInspector]
	public PortalCameraTexture[] portalCameraTextures = new PortalCameraTexture[2];
	public PortalCameraTexture portalCameraTextureA {
		get { return portalCameraTextures[0]; }
		set { portalCameraTextures[0] = value; }
	}
	public PortalCameraTexture portalCameraTextureB {
		get { return portalCameraTextures[1]; }
		set { portalCameraTextures[1] = value; }
	}

	// Cameras
	[HideInInspector]
	public Camera playerCamera;
	[HideInInspector]
	public Camera[] portalCameras = new Camera[2];
	public Camera portalCameraA {
		get { return portalCameras[0]; }
		set { portalCameras[0] = value; }
	}
	public Camera portalCameraB {
		get { return portalCameras[1]; }
		set { portalCameras[1] = value; }
	}

	// TeleportEnters
	[HideInInspector]
	public TeleportEnter[] portalTeleportEnters = new TeleportEnter[2];
	public TeleportEnter portalTeleportEnterA {
		get { return portalTeleportEnters[0]; }
		set { portalTeleportEnters[0] = value; }
	}
	public TeleportEnter portalTeleportEnterB {
		get { return portalTeleportEnters[1]; }
		set { portalTeleportEnters[1] = value; }
	}

	int portalLayer;

	// Use this for initialization
	void Start () {
		portalLayer = LayerMask.NameToLayer("HideFromPortal");
		portalA = transform;
		portalB = otherPortal;
		portalA.gameObject.layer = portalLayer;
		portalB.gameObject.layer = portalLayer;
		Debug.Assert(otherPortal != null, "Please set the other portal in the inspector.");
		Debug.Assert(otherPortal.GetComponent<Portal>() == null, "Only one portal needs the portal script attached.");
		Debug.Assert(otherPortal.GetComponent<Collider>() != null, "Ensure that the other portal has a collider as well.");

		for (int i = 0; i < 2; i++) {
			if (portals[i].GetComponent<EpitaphRenderer>() == null) portals[i].gameObject.AddComponent<EpitaphRenderer>();
			//MeshCollider mc = portals[i].GetComponent<MeshCollider>();
			//if (mc == null) mc = portals[i].gameObject.AddComponent<MeshCollider>();
			//mc.convex = true;
			//mc.isTrigger = true;
		}

		playerCamera = EpitaphScreen.instance.playerCamera;

		InitializePortalCameras();
		InitializePortalTeleporters();
	}

	void InitializePortalCameras() {
		for (int i = 0; i < 2; i++) {
			// Initialize GameObject
			string name = "PortalCamera" + ((char)('A' + i));
			GameObject newPortalCameraObj = new GameObject(name);
			newPortalCameraObj.transform.SetParent(portals[(i+1)%2], false);
			// Initialize Camera component
			portalCameras[i] = newPortalCameraObj.AddComponent<Camera>();
			portalCameras[i].CopyFrom(playerCamera);
			portalCameras[i].depth = -100;
			portalCameras[i].cullingMask &= ~(1 << portalLayer);
			// Initialize Edge Detection
			BladeEdgeDetection playerEdgeDetection = playerCamera.GetComponent<BladeEdgeDetection>();
			BladeEdgeDetection edgeDetection = newPortalCameraObj.AddComponent<BladeEdgeDetection>();
			edgeDetection.edgeColor = portalEdgeDetectionColor;
			edgeDetection.depthSensitivity = playerEdgeDetection.depthSensitivity;
			edgeDetection.normalSensitivity = playerEdgeDetection.normalSensitivity;
			// Initialize Global Fog
			GlobalFog playerFog = playerCamera.GetComponent<GlobalFog>();
			GlobalFog newFog = newPortalCameraObj.AddComponent<GlobalFog>();
			newFog.fogColor = playerFog.fogColor;
			newFog.distanceFog = playerFog.distanceFog;
			newFog.excludeFarPixels = playerFog.excludeFarPixels;
			newFog.useRadialDistance = playerFog.useRadialDistance;
			newFog.heightFog = playerFog.heightFog;
			newFog.heightDensity = playerFog.heightDensity;
			newFog.startDistance = playerFog.startDistance;
			newFog.fogShader = playerFog.fogShader;
			newFog.enabled = playerFog.enabled;
			// Initialize Fog
			ColorfulFog playerFog2 = playerCamera.GetComponent<ColorfulFog>();
			ColorfulFog newFog2 = newPortalCameraObj.AddComponent<ColorfulFog>();
			newFog2.useCustomDepthTexture = playerFog2.useCustomDepthTexture;
			newFog2.fogDensity = playerFog2.fogDensity;
			newFog2.distanceFog = playerFog2.distanceFog;
			newFog2.useRadialDistance = playerFog2.useRadialDistance;
			newFog2.heightFog = playerFog2.heightFog;
			newFog2.enabled = playerFog2.enabled;
			newFog2.height = playerFog2.height;
			newFog2.heightDensity = playerFog2.heightDensity;
			newFog2.startDistance = playerFog2.startDistance;
			newFog2.fogMode = playerFog2.fogMode;
			newFog2.fogStart = playerFog2.fogStart;
			newFog2.coloringMode = playerFog2.coloringMode;
			newFog2.fogCube = playerFog2.fogCube;
			newFog2.solidColor = playerFog2.solidColor;
			newFog2.skyColor = playerFog2.skyColor;
			newFog2.equatorColor = playerFog2.equatorColor;
			newFog2.groundColor = playerFog2.groundColor;
			newFog2.gradient = playerFog2.gradient;
			newFog2.gradientResolution = playerFog2.gradientResolution;
			newFog2.gradientTexture = playerFog2.gradientTexture;
			newFog2.fogShader = playerFog2.fogShader;
			newFog2.customDepthShader = playerFog2.customDepthShader;
			// Initialize Portal Camera Texture component
			portalCameraTextures[i] = newPortalCameraObj.AddComponent<PortalCameraTexture>();
			portalCameraTextures[i].portal = this;
			portalCameraTextures[i].portalIndex = i;
			// Initialize NoRenderZoneCollider -- RE-INTRODUCE WHEN FIXED
			//string noRenderZoneName = "PortalCameraNoRenderZone" + ((char)('A' + i));
			//GameObject noRenderZoneObj = new GameObject(noRenderZoneName);
			//PortalCameraNoRenderZone noRenderZone = noRenderZoneObj.AddComponent<PortalCameraNoRenderZone>();
			//noRenderZone.portalCam = portalCameraTextures[i];
		}
	}

	void InitializePortalTeleporters() {
		// Create new Teleporter GameObjects
		for (int i = 0; i < 2; i++) {
			string name = "PortalTeleporterTrigger" + ((char)('A' + i));
			GameObject newTeleporterObj = new GameObject(name);
			newTeleporterObj.transform.SetParent(portals[i], false);

			// Set up trigger colliders
			if (useMeshAsCollider) {
				MeshCollider newMeshCollider = newTeleporterObj.AddComponent<MeshCollider>();
				newMeshCollider.inflateMesh = true;
				Debug.Assert(portals[i].GetComponent<MeshFilter>() != null, "Trying to use mesh as collider without a mesh on the portal: " + portals[i].gameObject.name);
				newMeshCollider.sharedMesh = portals[i].GetComponent<MeshFilter>().mesh;
				newMeshCollider.convex = true;
				triggerColliders[i] = newMeshCollider;
			}
			else {
				BoxCollider newBoxCollider = newTeleporterObj.AddComponent<BoxCollider>();
				Mesh portalMesh = portals[i].GetComponent<MeshFilter>().mesh;
				Vector3 size = portalMesh.bounds.size;
				newBoxCollider.size = size;
				newBoxCollider.transform.localPosition = portalMesh.bounds.center;
				triggerColliders[i] = newBoxCollider;
			}
			// Set up normal vectors for portal colliders
			// Assumes that there is only one face for the portal
			portalForwards[i] = -portals[i].TransformVector(portals[i].GetComponent<MeshFilter>().mesh.normals[0]);
			triggerColliders[i].isTrigger = true;

			MeshCollider parentCollider = portals[i].GetComponent<MeshCollider>();
			if (parentCollider != null) parentCollider.enabled = false;
		}
		if (teleportOnEnter) {
			// Add components for teleporters
			for (int i = 0; i < 2; i++) {
				portalTeleportEnters[i] = triggerColliders[i].gameObject.AddComponent<TeleportEnter>();
			}
			// Initialize teleporter references
			for (int i = 0; i < 2; i++) {
				int otherIndex = (i + 1) % 2;
				portalTeleportEnters[i].teleportExit = portalTeleportEnters[otherIndex].GetComponent<Collider>();

				portalTeleportEnters[i].trigger.triggerCondition = MagicTrigger.TriggerConditionType.PlayerMovingDirection;
				portalTeleportEnters[i].trigger.targetDirection = portalForwards[i];
				portalTeleportEnters[i].trigger.playerFaceThreshold = 0.01f;
				if (i == 0) {
					portalTeleportEnters[i].trigger.OnMagicTriggerStayOneTime += SwapEdgeDetectionColorsA;
				}
				else {
					portalTeleportEnters[i].trigger.OnMagicTriggerStayOneTime += SwapEdgeDetectionColorsB;
				}
			}
			// Set up offsets after other reference intialization is complete
			for (int i = 0; i < 2; i++) {
				portalTeleportEnters[i].teleportOffset = -portalForwards[(i + 1) % 2] * portalOffset;
				triggerColliders[i].transform.position += portalForwards[0] * colliderOffset;
			}
		}
	}

	private void SwapEdgeDetectionColorsA(Collider unused) {
		SwapEdgeDetectionColors();
	}
	private void SwapEdgeDetectionColorsB(Collider unused) {
		SwapEdgeDetectionColors();
	}
	private void SwapEdgeDetectionColors() {
		BladeEdgeDetection playerED = playerCamera.GetComponent<BladeEdgeDetection>();
		BladeEdgeDetection portalAED = portalCameraA.GetComponent<BladeEdgeDetection>();
		BladeEdgeDetection portalBED = portalCameraB.GetComponent<BladeEdgeDetection>();

		Color temp = playerED.edgeColor;
		playerED.edgeColor = portalEdgeDetectionColor;
		portalEdgeDetectionColor = temp;
		portalAED.edgeColor = temp;
		portalBED.edgeColor = temp;
	}

	private void OnDisable() {
		if (portalTeleportEnterA != null && portalTeleportEnterA.trigger != null) {
			portalTeleportEnterA.trigger.OnMagicTriggerStayOneTime -= SwapEdgeDetectionColorsA;
		}
		if (portalTeleportEnterB != null && portalTeleportEnterB.trigger != null) {
			portalTeleportEnterB.trigger.OnMagicTriggerStayOneTime -= SwapEdgeDetectionColorsB;
		}
	}
}
