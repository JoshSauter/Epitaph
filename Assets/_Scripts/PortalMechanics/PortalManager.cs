﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EpitaphUtils;
using System.Linq;
using UnityStandardAssets.ImageEffects;

namespace PortalMechanics {
	public class PortalManager : Singleton<PortalManager> {
		public bool DEBUG = false;
		public LayerMask hideFromPortalCamera;
		DebugLogger debug;
		Dictionary<string, HashSet<Portal>> portalsByChannel = new Dictionary<string, HashSet<Portal>>();
		public List<Portal> activePortals = new List<Portal>();
		Camera portalCamera;

		void Awake() {
			debug = new DebugLogger(this, () => DEBUG);
		}

		void Start() {
			InitializeVirtualPortalCamera();
		}

		/// <summary>
		/// Adds a portal to the portal dictionary for this channel, and if the channel has two portals, enable the portal
		/// </summary>
		/// <param name="channelName"></param>
		/// <param name="portal"></param>
		public void AddPortal(string channelName, Portal portal, int portalsRequiredToActivate = 2) {
			if (!portalsByChannel.ContainsKey(channelName)) {
				portalsByChannel[channelName] = new HashSet<Portal>();
			}

			if (portalsByChannel[channelName].Count == portalsRequiredToActivate) {
				debug.LogError($"Channel {channelName} already has {portalsRequiredToActivate} portals! Check the channels for the following portals:\n" +
					string.Join("\n", portalsByChannel[channelName].Select(p => p.name).ToArray()) + "\n" + portal.name);
				return;
			}

			portalsByChannel[channelName].Add(portal);

			if (portalsByChannel[channelName].Count == portalsRequiredToActivate) {
				debug.Log("Enabling portals for channel: " + channelName);
				EnablePortalsForChannel(portalsByChannel[channelName]);
				foreach (var activePortal in portalsByChannel[channelName]) {
					activePortals.Add(activePortal);
				}
			}
		}

		/// <summary>
		/// Removes a portal from the portal dictionary, and disables portal
		/// </summary>
		/// <param name="channelName"></param>
		/// <param name="portal"></param>
		/// <returns>Returns true if the portal was successfully found and removed, false otherwise</returns>
		public bool RemovePortal(string channelName, Portal portal, int portalsRequiredToActivate = 2) {
			if (!portalsByChannel.ContainsKey(channelName)) {
				debug.LogWarning("Trying to remove a receiver for non-existent channel: " + channelName);
				return false;
			}

			bool receiverRemoved = portalsByChannel[channelName].Remove(portal);
			activePortals.Remove(portal);
			if (receiverRemoved && portalsByChannel[channelName].Count < portalsRequiredToActivate) {
				debug.Log("Disabling portal for channel " + channelName);
				DisablePortalsForChannel(portalsByChannel[channelName]);
				foreach (var inactivePortal in portalsByChannel[channelName]) {
					activePortals.Remove(inactivePortal);
				}
			}
			return receiverRemoved;
		}

		#region PortalEnableDisable
		void EnablePortalsForChannel(HashSet<Portal> portals) {
			foreach (var portal in portals) {
				Portal otherPortal = portals.FirstOrDefault(x => x != portal);
				// If this is a single portal system, just set otherPortal to self
				if (otherPortal == null) {
					otherPortal = portal;
				}
				portal.EnablePortal(otherPortal);
			}
		}

		void DisablePortalsForChannel(HashSet<Portal> portals) {
			foreach (var portal in portals) {
				portal.DisablePortal();
			}
		}
		#endregion

		/// <summary>
		/// Instantiates portalCamera as a copy of the player's Camera with post-processing effects copied, as a child of the PortalManager, and disables the Camera.
		/// </summary>
		void InitializeVirtualPortalCamera() {
			Camera playerCam = EpitaphScreen.instance.playerCamera;
			portalCamera = new GameObject("VirtualPortalCamera").AddComponent<Camera>();
			portalCamera.transform.SetParent(transform, false);
			portalCamera.enabled = false;

			// Copy main camera component from player's camera
			portalCamera.CopyFrom(playerCam);
			portalCamera.cullingMask &= ~hideFromPortalCamera;
			portalCamera.backgroundColor = Color.white;

			VirtualPortalCamera virtualPortalCam = portalCamera.gameObject.AddComponent<VirtualPortalCamera>();
			virtualPortalCam.DEBUG = DEBUG;

			// Copy post-process effects from player's camera
			// Order of components here matters; it affects the rendering order of the postprocess effects
			//portalCamera.gameObject.PasteComponent(playerCam.GetComponent<BloomOptimized>());                                          // Copy Bloom
			ColorfulFog fog = portalCamera.gameObject.PasteComponent(playerCam.GetComponent<ColorfulFog>());                            // Copy Fog
			BladeEdgeDetection edgeDetection = portalCamera.gameObject.PasteComponent(playerCam.GetComponent<BladeEdgeDetection>());   // Copy Edge Detection (maybe change color)
			//edgeDetection.enabled = false;
			//edgeDetection.checkPortalDepth = false;
			virtualPortalCam.postProcessEffects.Add(fog);
			virtualPortalCam.postProcessEffects.Add(edgeDetection);

			portalCamera.gameObject.name = "VirtualPortalCamera";
		}
	}
}
