﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

[RequireComponent (typeof (SphereCollider))]
[RequireComponent (typeof (BoxCollider))]
[RequireComponent (typeof (Rigidbody))]
[RequireComponent (typeof (AudioSource))]
[RequireComponent (typeof (ThingMotor))]
public class Thing : MonoBehaviour {

    protected class Settings {
        internal int cameraOffset = 15;
        internal float acceleration = 4;
        internal float drag = 1.8f;
        internal float mass = 0.2f;
        internal float getNewDestinationInterval = 5;
        internal int newDestinationRange = 40;
        internal bool alwaysFacingTarget = true;
        internal Color myCubeColor;

        public Settings () {
            cameraOffset = 15;
            acceleration = 4;
            drag = 1.8f;
            mass = 0.2f;
            getNewDestinationInterval = 5;
            newDestinationRange = 40;
            alwaysFacingTarget = true;
        }
    }

    protected Settings settings { get; set; }

    protected bool InWater { get; private set; }
    protected int NeighborCount { get { return neighborList.Count; } }

    private float speakCDLength;
    private bool speakInCD;
    private bool stopWalkingAround;
    private bool stopTalking;
    private ThingMotor motor;
    private SphereCollider neighborDetector;
    private ChatBalloon chatBalloon;
    private ParticleSystem explodePS;
    private AudioSource audioSource;
    private List<GameObject> neighborList;
    private string soundFilePath = "Sounds/";
    private Color originalColor;
    private Renderer rend;
    private static GameObject generatedCubeContainer;
    public int DesiredFollowDistance { get { return settings.cameraOffset; } }

    private void OnEnable () {
        TTTEventsManager.OnSomeoneSpeaking += OnSomeoneSpeaking;
        TTTEventsManager.OnSomeoneSparking += OnSomeoneSparking;
        TOD_Data.OnSunset += OnSunset;
        TOD_Data.OnSunrise += OnSunrise;
    }

    private void OnDisable () {
        TTTEventsManager.OnSomeoneSpeaking -= OnSomeoneSpeaking;
        TTTEventsManager.OnSomeoneSparking -= OnSomeoneSparking;
        TOD_Data.OnSunset -= OnSunset;
        TOD_Data.OnSunrise -= OnSunrise;
    }

    private void Awake () {
        settings = new Settings ();
        gameObject.tag = "Thing";
        neighborList = new List<GameObject> ();
        TTTAwake ();
    }

    private void Start () {
        //neighbor detector
        neighborDetector = GetComponent<SphereCollider> ();
        neighborDetector.isTrigger = true;

        //motor
        motor = GetComponent<ThingMotor> ();
        motor.SetAccel (settings.acceleration);
        motor.rb.drag = settings.drag;
        motor.rb.mass = settings.mass;
        motor.FacingTarget (settings.alwaysFacingTarget);

        //Chat Ballon
        chatBalloon = gameObject.GetComponentInChildren<ChatBalloon> ();
        speakCDLength = Random.Range (8f, 13f);

        //Instantiating Particle Object
        explodePS = GetComponentInChildren<ParticleSystem> ();

        //Sound
        audioSource = gameObject.GetComponent<AudioSource> ();
        audioSource.spatialBlend = 0.9f;
        audioSource.maxDistance = 35;

        //color
        rend = GetComponent<Renderer> ();
        if (rend == null) rend = GetComponentInChildren<Renderer> ();
        originalColor = rend.material.color;

        TTTStart ();
    }
    private void Update () {
        if (transform.position.y < -9 || transform.position.y > 157) {
            ResetPosition ();
        }
        TTTUpdate ();
    }

    private void OnSomeoneSpeaking (GameObject who) {
        if (neighborList.Contains (who)) {
            OnNeighborSpeaking ();
        }
    }

    private void OnSomeoneSparking (GameObject who) {
        if (neighborList.Contains (who)) {
            OnNeigborSparkingParticles ();
        }
    }

    private void OnTriggerEnter (Collider other) {
        if (other.gameObject.CompareTag ("Thing")) {
            OnMeetingSomeone (other.gameObject);
            ThingConsole.Log (gameObject.name + " is meeting " + other.name);
            if (!neighborList.Contains (other.gameObject)) {
                neighborList.Add (other.gameObject);
            }
        }
    }

    private void OnTriggerExit (Collider other) {
        if (other.gameObject.CompareTag ("Thing")) {
            OnLeavingSomeone (other.gameObject);
            if (neighborList.Contains (other.gameObject)) {
                neighborList.Remove (other.gameObject);
            }
        }
    }

    private void RescueFromWater () {
        if (InWater) {
            ResetPosition ();
        }
    }

    private void UnlockSpeakCD () {
        speakInCD = false;
    }

    //TODO: not to be called directly by other classes
    internal void OnWaterEnter () {
        InWater = true;
        Invoke ("RescueFromWater", 60f);
        OnTouchWater ();
    }

    internal void OnWaterExit () {
        InWater = false;
        OnLeaveWater ();
        ThingConsole.Log (gameObject.name + " left water.");
    }

    protected void SetTarget (Vector3 target) {
        if (!stopWalkingAround) {
            motor.SetTarget (target);
        }
    }

    protected void StopMoving () {
        stopWalkingAround = true;
        motor.Stop ();
        ThingConsole.Log (gameObject.name + " stopped moving");
    }

    protected void StopMoving (float seconds) {
        StopMoving ();
        Invoke ("RestartWalking", seconds);
    }

    protected void Mute () {
        stopTalking = true;
        ThingConsole.LogWarning (gameObject.name + "is being muted");
    }

    protected void DeMute () {
        stopTalking = false;
        ThingConsole.LogWarning (gameObject.name + "can speak again.");
    }

    protected void RestartWalking () {
        stopWalkingAround = false;
    }

    protected void SetRandomTarget (float area) {
        SetTarget (new Vector3 (Random.Range (-area, area), 0, Random.Range (-area, area)));
    }

    protected void AddForce (Vector3 f) {
        motor.rb.AddForce (f);
    }

    protected void SetScale (Vector3 newScale) {
        transform.localScale = newScale;
    }

    protected void Speak (string content, float stayLength) {
        if (!speakInCD || !stopTalking) {
            TTTEventsManager.main.SomeoneSpoke (gameObject);
            chatBalloon.SetTextAndActive (content, stayLength);
            speakInCD = true;
            Invoke ("UnlockSpeakCD", speakCDLength);
        }
        ThingConsole.Log (gameObject.name + "is speaking: " + content);
    }

    protected void Speak (string content) {
        Speak (content, 2f);
    }

    protected void Spark (Color particleColor, int numberOfParticles) {
        var particleMain = explodePS.main;
        particleMain.startColor = particleColor;
        var newBurst = new ParticleSystem.Burst (0f, numberOfParticles);
        explodePS.emission.SetBurst (0, newBurst);
        explodePS.Play ();
        TTTEventsManager.main.SomeoneSparked (gameObject);
    }

    protected void CreateCube () {
        GameObject acube = GameObject.CreatePrimitive (PrimitiveType.Cube);

        acube.transform.localScale = Vector3.one / 4;
        acube.transform.position = transform.position;

        if (generatedCubeContainer == null) {
            generatedCubeContainer = new GameObject ();
            generatedCubeContainer.name = "Generated Cube Container";
            generatedCubeContainer.AddComponent<ChildrenCounter> ();
        }

        acube.transform.parent = generatedCubeContainer.transform;
        generatedCubeContainer.GetComponent<ChildrenCounter> ().list.Add (acube);

        acube.AddComponent<Rigidbody> ();
        acube.AddComponent<ProducedCube> ().Init (settings.myCubeColor);
        //Hud.main.OneMoreCube();
    }

    protected void ResetColor () {
        rend.material.color = originalColor;
        ThingConsole.Log (gameObject.name + "reset its color");
    }

    protected void ChangeColor (Color c) {
        rend.material.color = c;
    }

    protected void PlaySound (string soundName) {
        audioSource.clip = Resources.Load (soundFilePath + soundName) as AudioClip;
        if (audioSource.clip != null) {
            audioSource.Play ();
        }

    }

    protected ThingMotor GetMotor () {
        return motor;
    }

    protected void RandomSetDestination () {
        SetRandomTarget (settings.newDestinationRange);
    }

    protected void ResetPosition () {
        motor.rb.position = ThingManager.main.transform.position;
        ThingConsole.Log (gameObject.name + " position was reset");
    }

    //VIRTUAL
    protected virtual void TTTAwake () { }
    protected virtual void TTTStart () { }
    protected virtual void TTTUpdate () { }
    protected virtual void OnMeetingSomeone (GameObject other) { }
    protected virtual void OnLeavingSomeone (GameObject other) { }
    protected virtual void OnNeighborSpeaking () { }
    protected virtual void OnNeigborSparkingParticles () { }
    protected virtual void OnTouchWater () { }
    protected virtual void OnLeaveWater () { }
    protected virtual void OnSunset () { }
    protected virtual void OnSunrise () { }

}