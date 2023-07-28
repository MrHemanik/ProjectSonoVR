using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public VolumeGenerationManager gm;

    public void OnLeftTrigger(InputAction.CallbackContext context)
    {
        if (!context.started) return; //Only work when initial click
        Debug.Log("VolumeCount in AnswerBox "+gm.abm.GetInsideVolumes().Count);
        if (gm.abm.GetInsideVolumes().Count == 1 && gm.activeRound)
        {
            gm.CheckAnswer(gm.abm.GetInsideVolumes()[0]);
        }
    }
    
}
