using Sandbox;
using Sandbox.Diagnostics;
using System;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using static Sandbox.Component;

public sealed class FreecamController : Component
{

	public enum Tracking
	{
		/// <summary>
		/// Don't track the target, even if one is set.
		/// </summary>
		Disabled = 0,

		/// <summary>
		/// Always keep the camera at the same relative position to the target.
		/// </summary>
		Follow = 1,

		/// <summary>
		/// Always keep the camera looking at the target.
		/// </summary>
		LookAt = 2,
	}

	#region Fields

	private Angles targetRotation;
	private Vector3 targetPosition;

	#endregion

	#region Properties

	/// <summary>
	/// Wether or not to use a specific FOV instead of the player's preference.
	/// </summary>
	[Property]
	public bool OverrideFov { get; set; } = false;

	/// <summary>
	/// The field of view of the camera, if <see cref="OverrideFov"/> is set to <c>true</c>.
	/// </summary>
	[Property, HideIf(nameof(OverrideFov), false)]
	public float Fov { get; set; } = 90f;

	/// <summary>
	/// Movement speed of the camera.
	/// </summary>
	[Property]
	public float Speed { get; set; } = 600f;

	public float SprintSpeedMultiplier { get; set; } = 2.0f;

	/// <summary>
	/// The camera component to control.
	/// </summary>
	[Property]
	public CameraComponent Camera { get; set; }

	/// <summary>
	/// A <see cref="GameObject"/> to track with the camera."/>
	/// </summary>
	[Property]
	GameObject TrackingTarget { get; set; }

	/// <summary>
	/// Type of tracking to use.
	/// </summary>
	[Property]
	Tracking TrackingType { get; set; } = Tracking.LookAt;

	/// <summary>
	/// A value indicating whether the camera should be smoothed.
	/// </summary>
	[Property]
	public bool Smoothed { get; set; } = false;

	/// <summary>
	/// The smoothing factor of the camera. Only used if <see cref="Smoothed"/> is set to <c>true</c>.
	/// </summary>
	[Property, Range(1f, 1000f, 0.005f), HideIf(nameof(Smoothed), false)]
	public float SmoothingFactor { get; set; } = 100f;

	#endregion

	#region Component Lifecycle

	protected override void OnAwake()
	{
		Camera ??= GameObject.Components.GetInDescendantsOrSelf<CameraComponent>();

		if (Camera is null)
		{
			return;
		}

		targetRotation = Camera.Transform.Rotation;
		targetPosition = Camera.Transform.Position;
	}

	protected override void OnUpdate()
	{

		if ( Camera is null )
		{
			return;
		}

		UpdateTargetPosition();
		UpdateTargetRotation();
		UpdateSpeed();

		if ( Smoothed )
		{
			var smoothing = 1f / SmoothingFactor;
			Camera.Transform.Position = Vector3.Lerp( Camera.Transform.Position, targetPosition, smoothing );
			Camera.Transform.Rotation = Angles.Lerp( Camera.Transform.Rotation, targetRotation, smoothing ).WithRoll( 0f );
		}
		else
		{
			Camera.Transform.Position = targetPosition;
			Camera.Transform.Rotation = targetRotation;
		}

		Camera.FieldOfView = OverrideFov ? Fov : Preferences.FieldOfView;
	}

	#endregion

	#region Methods

	private void UpdateSpeed()
	{
		if ( Input.MouseWheel.IsNearZeroLength )
			return;

		Speed = Math.Clamp( Speed + Input.MouseWheel.y * 10f, 10f, 5000f );
	}

	private void UpdateTargetRotation()
	{
		if ( TrackingTarget is not null)
		{
			if ( TrackingType == Tracking.LookAt )
			{
				// Calculate the direction to the target.
				Vector3 direction = (TrackingTarget.Transform.Position - Camera.Transform.Position).Normal;
				Rotation lookAt = Rotation.LookAt( direction, Vector3.Up );

				targetRotation = lookAt;
				return;
			}
		}

		if ( !Input.AnalogLook.IsNearlyZero() )
		{
			targetRotation += Input.AnalogLook;
			targetRotation = targetRotation.WithPitch( targetRotation.pitch.Clamp( -89.9f, 89.9f ) );
		}
	}

	private void UpdateTargetPosition()
	{
		var currentRotation = Camera.Transform.Rotation;
		var forward = Rotation.FromYaw( currentRotation.Yaw() );
		var speed = Input.Down( "Sprint" ) ? Speed * SprintSpeedMultiplier : Speed;

		var analogMove = Input.AnalogMove.Normal;
		var rotatedMove = analogMove.RotateAround( Vector3.Down, forward );
		targetPosition += rotatedMove * speed * Time.Delta;

		if ( Input.Down( "Jump" ) )
		{
			targetPosition += Vector3.Up * (speed / 2) * Time.Delta;
		}

		if ( Input.Down( "Crouch" ) )
		{
			targetPosition -= Vector3.Up * (speed / 2) * Time.Delta;
		}
	}

	#endregion
}
