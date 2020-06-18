﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PowerTrailMechanics {
    public class TriggerPowerTrailFromButton : MonoBehaviour {
        public enum PowerControl {
            powerOnAndOff,
            powerOnOnly,
            powerOffOnly
        }
        public Button button;
        public PowerControl whatToControl;
        PowerTrail thisPowerTrail;

        void Start() {
            thisPowerTrail = GetComponent<PowerTrail>();
            if (whatToControl == PowerControl.powerOnAndOff || whatToControl == PowerControl.powerOnOnly) {
                button.OnButtonPressBegin += (Button b) => thisPowerTrail.powerIsOn = true;
            }
            if (whatToControl == PowerControl.powerOnAndOff || whatToControl == PowerControl.powerOffOnly) {
                button.OnButtonDepressFinish += (Button b) => thisPowerTrail.powerIsOn = false;
            }
        }
    }
}