﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EpitaphUtils;
using System.Net.Mime;
using System.Linq;

namespace PowerTrailMechanics {
	[System.Serializable]
	public class NodeTrailInfo {
		public Node parent;
		public Node thisNode;
		public float startDistance;
		public float endDistance;
	}

	public class PowerTrail : MonoBehaviour {
		public bool DEBUG = false;
		public DebugLogger debug;

		public enum PowerTrailState {
			depowered,
			partiallyPowered,
			powered
		}

		Material material;
		public NodeSystem powerNodes;
		public List<NodeTrailInfo> trailInfo = new List<NodeTrailInfo>();

		// Data to be sent to the GPU. Positions are not repeated. Index order matches node ID
		const int MAX_NODES = 128;
		Vector4[] nodePositions;        // Positions of each node. w value is unused and ignored
		int[] endPositionIDs;
		int[] startPositionIDs;
		float[] interpolationValues;    // [0-1] interpolation value between startPosition and endPosition for each trail. Only GPU data that changes at runtime
		const string nodePositionsKey = "_NodePositions";
		const string startPositionIDsKey = "_StartPositionIDs";
		const string endPositionIDsKey = "_EndPositionIDs";
		const string interpolationValuesKey = "_InterpolationValues";
		const string sdfCapsuleRadiusKey = "_CapsuleRadius";


		public float speed = 1f;
		float powerTrailRadius = 0.15f;

		#region events
		public delegate void PowerTrailAction();
		public event PowerTrailAction OnPowerBegin;
		public event PowerTrailAction OnPowerFinish;
		public event PowerTrailAction OnDepowerBegin;
		public event PowerTrailAction OnDepowerFinish;
		#endregion

		///////////
		// State //
		///////////
		public float distance = 0f;
		public float maxDistance = 0f;
		public bool powerIsOn = false;
		[SerializeField]
		private PowerTrailState _state = PowerTrailState.depowered;
		public PowerTrailState state {
			get { return _state; }
			set {
				if (_state == PowerTrailState.depowered && value == PowerTrailState.partiallyPowered) {
					OnPowerBegin?.Invoke();
				}
				else if (_state == PowerTrailState.partiallyPowered && value == PowerTrailState.powered) {
					OnPowerFinish?.Invoke();
				}
				else if (_state == PowerTrailState.powered && value == PowerTrailState.partiallyPowered) {
					OnDepowerBegin?.Invoke();
				}
				else if (_state == PowerTrailState.partiallyPowered && value == PowerTrailState.depowered) {
					OnDepowerFinish?.Invoke();
				}
				_state = value;
			}
		}

		private void Awake() {
			if (powerNodes == null) {
				powerNodes = GetComponent<NodeSystem>();
			}
		}

		void Start() {
			material = GetComponent<Renderer>().material;
			debug = new DebugLogger(this, () => DEBUG);
			PopulateTrailInfo();
			PopulateStaticGPUInfo();
		}

		void Update() {
			if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown("t")) {
				powerIsOn = !powerIsOn;
			}

			float prevDistance = distance;
			float nextDistance = NextDistance();
			if (nextDistance == prevDistance) return;

			// DEBUG: Remove this from Update after debugging
			PopulateStaticGPUInfo();

			UpdateInterpolationValues(nextDistance);

			UpdateState(prevDistance, nextDistance);
			distance = nextDistance;
		}

		void PopulateStaticGPUInfo() {
			nodePositions = new Vector4[MAX_NODES];
			interpolationValues = new float[MAX_NODES];
			startPositionIDs = new int[MAX_NODES];
			endPositionIDs = new int[MAX_NODES];

			for (int i = 0; i < MAX_NODES && i < powerNodes.Count; i++) {
				nodePositions[i] = transform.TransformPoint(powerNodes.GetNode(i).pos);
			}
			material.SetVectorArray(nodePositionsKey, nodePositions);

			for (int i = 0; i < MAX_NODES && i < trailInfo.Count; i++) {
				NodeTrailInfo trailInfoAtIndex = trailInfo[i];

				startPositionIDs[i] = trailInfoAtIndex.parent.id;
				endPositionIDs[i] = trailInfoAtIndex.thisNode.id;
			}

			material.SetFloatArray(startPositionIDsKey, startPositionIDs.Select(i => (float)i).ToArray());
			material.SetFloatArray(endPositionIDsKey, endPositionIDs.Select(i => (float)i).ToArray());
			material.SetFloat(sdfCapsuleRadiusKey, powerTrailRadius);
		}

		void PopulateTrailInfo() {
			PopulateTrailInfoRecursively(powerNodes.parentNode, 0);
		}

		void PopulateTrailInfoRecursively(Node curNode, float curDistance) {
			if (!curNode.isRootNode) {
				Node parentNode = powerNodes.GetNode(curNode.parentId);
				// If there is a parent node, add trail info here
				NodeTrailInfo info = new NodeTrailInfo {
					parent = parentNode,
					thisNode = curNode,
					startDistance = curDistance,
					endDistance = curDistance + (curNode.pos - parentNode.pos).magnitude
				};
				trailInfo.Add(info);

				// Update maxDistance as you add new trail infos
				if (info.endDistance > maxDistance) {
					maxDistance = info.endDistance;
				}

				// Recurse for each child
				if (!curNode.isLeafNode) {
					foreach (int childId in curNode.childrenIds) {
						PopulateTrailInfoRecursively(powerNodes.GetNode(childId), info.endDistance);
					}
				}
			}
			// Base case of root parent node
			else {
				foreach (int childId in curNode.childrenIds) {
					PopulateTrailInfoRecursively(powerNodes.GetNode(childId), curDistance);
				}
			}
		}

		void UpdateInterpolationValues(float newDistance) {
			for (int i = 0; i < MAX_NODES && i < trailInfo.Count; i++) {
				NodeTrailInfo infoAtIndex = trailInfo[i];
				interpolationValues[i] = Mathf.Clamp01(Mathf.InverseLerp(infoAtIndex.startDistance, infoAtIndex.endDistance, newDistance));
			}
			material.SetFloatArray(interpolationValuesKey, interpolationValues);
		}

		void UpdateState(float prevDistance, float nextDistance) {
			if (powerIsOn) {
				if (prevDistance == 0 && nextDistance > 0) {
					state = PowerTrailState.partiallyPowered;
				}
				else if (prevDistance < maxDistance && nextDistance == maxDistance) {
					state = PowerTrailState.powered;
				}
			}
			else if (!powerIsOn) {
				if (prevDistance == maxDistance && nextDistance < maxDistance) {
					state = PowerTrailState.partiallyPowered;
				}
				else if (prevDistance > 0 && nextDistance == 0) {
					state = PowerTrailState.depowered;
				}
			}
		}

		float NextDistance() {
			if (powerIsOn && distance < maxDistance) {
				return Mathf.Min(maxDistance, distance + Time.deltaTime * speed);
			}
			else if (!powerIsOn && distance > 0) {
				return Mathf.Max(0, distance - Time.deltaTime * speed);
			}
			else return distance;
		}

		#region EditorGizmos
		bool editorGizmosEnabled = false;
		public static float gizmoSphereSize = 0.05f;
		private void OnDrawGizmos() {
			if (powerNodes == null) {
				powerNodes = GetComponent<NodeSystem>();
			}

			if (powerNodes == null || powerNodes.parentNode == null || !editorGizmosEnabled) return;

			DrawGizmosRecursively(powerNodes.parentNode);
		}

		Color unselectedColor = new Color(.15f, .85f, .25f);
		Color selectedColor = new Color(.95f, .95f, .15f);
		void DrawGizmosRecursively(Node curNode) {
			Gizmos.color = (curNode == powerNodes.selectedNode) ? selectedColor : unselectedColor;

			foreach (int childId in curNode.childrenIds) {
				Node child = powerNodes.GetNode(childId);
				if (child != null) {
					DrawWireBox(curNode.pos, child.pos);
				}
			}
			foreach (int childId in curNode.childrenIds) {
				Node child = powerNodes.GetNode(childId);
				if (child != null) {
					DrawGizmosRecursively(child);
				}
			}
		}

		void DrawWireBox(Vector3 n1, Vector3 n2) {
			float halfBoxSize = powerTrailRadius / 2f;
			Vector3 diff = n2 - n1;
			Vector3 absDiff = new Vector3(Mathf.Abs(diff.x), Mathf.Abs(diff.y), Mathf.Abs(diff.z));

			Vector3 bl, br, tl, tr;
			if (absDiff.x > absDiff.y && absDiff.x > absDiff.z) {
				bl = new Vector3(0, -1, -1);
				br = new Vector3(0, -1, 1);
				tl = new Vector3(0, 1, -1);
				tr = new Vector3(0, 1, 1);
			}
			else if (absDiff.y > absDiff.x && absDiff.y > absDiff.z) {
				bl = new Vector3(-1, 0, -1);
				br = new Vector3(-1, 0, 1);
				tl = new Vector3(1, 0, -1);
				tr = new Vector3(1, 0, 1);
			}
			else {
				bl = new Vector3(-1, -1, 0);
				br = new Vector3(-1, 1, 0);
				tl = new Vector3(1, -1, 0);
				tr = new Vector3(1, 1, 0);
			}

			Vector3[] from = new Vector3[4] {
			n1 - bl * halfBoxSize,
			n1 - br * halfBoxSize,
			n1 - tl * halfBoxSize,
			n1 - tr * halfBoxSize
		};
			Vector3[] to = new Vector3[4] {
			n2 - bl * halfBoxSize,
			n2 - br * halfBoxSize,
			n2 - tl * halfBoxSize,
			n2 - tr * halfBoxSize
		};

			Vector3 direction = diff.normalized;
			for (int i = 0; i < 4; i++) {
				Gizmos.DrawLine(from[i], to[i]);
			}
		}
#endregion
	}
}