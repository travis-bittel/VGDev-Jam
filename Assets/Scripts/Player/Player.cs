﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerFacing
{
	UP,
	LEFT,
	RIGHT,
	DOWN
}

public enum PlayerState
{
	WALK,
	IDLE,
	DIE,
	OTHER
}

public class Player : SpoonListener
{
	#region Singleton Code
	private static Player _instance;

	public static Player Instance { get { return _instance; } }

	private void Awake()
	{
		if (_instance != null && _instance != this)
		{
			Debug.LogError("Attempted to Instantiate multiple Players in one scene!");
			Destroy(gameObject);
		}
		else
		{
			_instance = this;
		}

		anim = GetComponent<Animator>();
	}

	private void OnDestroy()
	{
		if (this == _instance) { _instance = null; }
	}
	#endregion

	public PlayerState state = PlayerState.IDLE;
	public float speedScalar;
	public Vector3 velocity;
	private VisionMode _heldJamColor; // The color of the jam the player is currently carrying
	public VisionMode heldJamColor
    {
		get
        {
			return _heldJamColor;
        }
		set
        {
			_heldJamColor = value;
			JarBehaviour.instance.pickUp(value);
        }
    }

	[SerializeField]
	private Rigidbody2D rb;

	[SerializeField]
	// The object the player will interact with if they press the Interact Key.
	// Based on interactionPriority of the object if multiple are within interaction range.
	private InteractableObject currentInteractableObject;

	public bool canMove;

	// Start is called before the first frame update

	// [SerializeField]
	private Animator anim;
	private BoxCollider2D box;
	[Range(0.01f, 1f)]
	public float mAnimSpd;

	GameObject hintText;

	private float _remainingVisionDuration;
	public float RemainingVisionDuration
    {
		get { return _remainingVisionDuration; }
		set { _remainingVisionDuration = value; }
    }

	[SerializeField]
	private LineRenderer lineRenderer;
	

	void Start()
	{
		if (speedScalar == 0)
        {
			Debug.LogWarning("Speed was set to 0, defaulting to 0.05");
        }
		state = PlayerState.IDLE;
		velocity = new Vector3(0, 0, 0);

		if (rb == null)
        {
			rb = GetComponent<Rigidbody2D>();
		}

		if (TextManager.Instance != null)
        {
			TextManager.Instance.DisplayFixedText(Color.white, "Use Enter to dismiss text.", "Use WASD to move.");
		}
		hintText = transform.GetChild(0).gameObject;
		box = GetComponent<BoxCollider2D>();
	}

	Vector3 yAxis = new Vector3(0, 1, 0);
	// Update is called once per frame
	void Update()
	{
		// End of frame:
		if (canMove)
        {
			Vector2 newPosition = new Vector2(transform.position.x + velocity.x * speedScalar, transform.position.y + velocity.y * speedScalar);
			rb.MovePosition(newPosition);
			anim.speed = velocity.magnitude / speedScalar * mAnimSpd;
		}
		else
        {
			anim.speed = 0;
		}

		if (hintText != null)
		{
			hintText.transform.rotation = Quaternion.identity;
			if (transform.localScale.x != 0.0f)
				hintText.transform.localScale = Vector3.one * (1.0f / transform.localScale.x);
			hintText.transform.position = transform.position + yAxis * (box.bounds.size.y);
		}
	}

	// Happens onPress and Release.
	public void OnMove(InputValue value)
	{
		if (canMove)
        {
			Vector2 moveDir = value.Get<Vector2>();
			velocity = new Vector3(moveDir.x, moveDir.y, 0);

			// We only want to change directions when the player has input a direction (ie. not when the button is released)
			if (velocity != Vector3.zero)
            {
				SetFacingDirectionFromVelocity();
			}
		} else
        {
			velocity = Vector3.zero;
        }
	}

	[FMODUnity.EventRef]
	public string general_interaction, highlight_interaction;

	public void OnInteract()
    {
		if (currentInteractableObject != null)
        {
			currentInteractableObject.OnInteract();
			if (!(currentInteractableObject is ButtonObject))
				FMODUnity.RuntimeManager.PlayOneShot(highlight_interaction, transform.position);
		}
	}


	public void OnAdvanceText()
    {
		if (TextManager.Instance.currentParagraph != null)
		{
			FMODUnity.RuntimeManager.PlayOneShot(general_interaction, transform.position);
		}
		TextManager.Instance.NextSentence();
	}

	public void OnCloseGame()
    {
		Application.Quit();
    }

	[FMODUnity.EventRef]
	public string vision_on, vision_off;
	public void OnToggleVisionModeOn()
    {
		GameManager.Instance.CurrentVisionMode = heldJamColor;
		heldJamColor = VisionMode.DEFAULT; // Remove jam jar whenever the player uses any amount of jam
		if (VisionDisplayHandler.Instance == null)
        {
			Debug.LogError("AAAA");
        }
		VisionDisplayHandler.Instance.UpdateFillColor();
		StartCoroutine(OnToggleVisionModeOff());
		FMODUnity.RuntimeManager.PlayOneShot(vision_on);
	}

	IEnumerator OnToggleVisionModeOff()
    {
		_remainingVisionDuration = SpoonBehaviour.Instance.jam * 5;
		Debug.Log("jam vision effect will last for " + _remainingVisionDuration + " seconds");
		while (_remainingVisionDuration > 0)
        {
			_remainingVisionDuration -= Time.deltaTime;
			yield return null;
		}
		//yield return new WaitForSeconds(SpoonBehaviour.Instance.jam * 5);
		_remainingVisionDuration = 0;
		GameManager.Instance.CurrentVisionMode = VisionMode.DEFAULT;
		FMODUnity.RuntimeManager.PlayOneShot(vision_off);
	}

	public void OnToggleVisionMode()
    {
		if (GameManager.Instance.CurrentVisionMode != heldJamColor)
        {
			GameManager.Instance.CurrentVisionMode = heldJamColor;
		} else
        {
			GameManager.Instance.CurrentVisionMode = VisionMode.DEFAULT;
		}
    }

	private void SetFacingDirectionFromVelocity()
	{
		if (velocity.x < 0)
		{
			transform.rotation = Quaternion.Euler(0, 0, 90);
		}
		else if (velocity.x > 0)
		{
			transform.rotation = Quaternion.Euler(0, 0, 270);
		}
		else if (velocity.y > 0)
		{
			transform.rotation = Quaternion.Euler(0, 0, 0);
		}
		else
		{
			transform.rotation = Quaternion.Euler(0, 0, 180);
		}
	}

	private void OnTriggerEnter2D(Collider2D collision)
    {
		// for spoon collision
		if (HandleSpoonCollision(collision)) {
			return;
        }

		InteractableObject obj = collision.gameObject.GetComponent<InteractableObject>();
		if (obj != null)
        {
			if (currentInteractableObject == null || obj.interactionPriority > currentInteractableObject.interactionPriority)
            {
				currentInteractableObject = obj;

				if (obj is JamJar)
                {
					heldJamColor = ((JamJar)obj).type;
                }
			}
		}

		// If colliding with another room.
		GameObject other = collision.gameObject;
		if (other.CompareTag("Room"))
		{
			GameManager.Instance.queuedRoom = other.GetComponent<Room>();
		}
		if (other.CompareTag("FinalJam"))
		{
			CanvasScript Screen = CanvasScript.Instance;
			Screen.nextScene = "CreditScene";
			Screen.ready = true;
			Screen.OnAdvanceText();
		}
	}
	private void OnTriggerExit2D(Collider2D collision)
	{
		InteractableObject obj = collision.gameObject.GetComponent<InteractableObject>();
		if (currentInteractableObject == obj)
		{
			currentInteractableObject = null;
		}

		GameManager gm = GameManager.Instance;
		// If colliding with another room...
		GameObject other = collision.gameObject;
		if (other.CompareTag("Room"))
		{
			Room nextRoom = other.GetComponent<Room>();
			if (nextRoom == gm.queuedRoom)
			{
				gm.roomChangeEnd(nextRoom);
			}
			gm.changeRooms();
		}
	}

	[FMODUnity.EventRef]
	public string footstep;

	public void PlayFootStep()
    {
		if (canMove)
			FMODUnity.RuntimeManager.PlayOneShot(footstep, transform.position);
    }

	public float remainingLineVisibilityTime;

	public void DrawLineBetweenPlayerAndLocation(Vector3 position, float duration = 1)
    {
		StartCoroutine(DrawLineCoroutine(position, duration));
	}

	public void AddPositionToExistingLine(Vector3 position, float durationToResetTo = 1)
    {
		Vector3[] existingPositions = new Vector3[lineRenderer.positionCount + 1];
		lineRenderer.GetPositions(existingPositions);
		existingPositions[existingPositions.Length - 1] = position;

		lineRenderer.SetPositions(existingPositions);
		remainingLineVisibilityTime = durationToResetTo;
	}

	private IEnumerator DrawLineCoroutine(Vector3 position, float duration)
    {
		remainingLineVisibilityTime = duration;
		lineRenderer.SetPositions(new Vector3[] { transform.position, position });
		lineRenderer.enabled = true;
		while (remainingLineVisibilityTime > 0)
        {
			remainingLineVisibilityTime -= Time.deltaTime;
			yield return null;
		}
		lineRenderer.enabled = false;
	}
}
