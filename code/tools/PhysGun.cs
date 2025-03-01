﻿using Sandbox;

[Library( "physgun" )]
public partial class PhysGun : BaseCarriable, IPlayerControllable, IFrameUpdate, IPlayerInput
{
	public override string ViewModelPath => "weapons/rust_pistol/v_rust_pistol.vmdl";

	protected PhysicsBody holdBody;
	protected WeldJoint holdJoint;

	protected PhysicsBody heldBody;
	protected Vector3 heldPos;
	protected Rotation heldRot;

	protected float holdDistance;
	protected bool grabbing;

	protected virtual float MinTargetDistance => 0.0f;
	protected virtual float MaxTargetDistance => 10000.0f;
	protected virtual float LinearFrequency => 20.0f;
	protected virtual float LinearDampingRatio => 1.0f;
	protected virtual float AngularFrequency => 20.0f;
	protected virtual float AngularDampingRatio => 1.0f;
	protected virtual float TargetDistanceSpeed => 50.0f;
	protected virtual float RotateSpeed => 0.2f;

	[Net] public bool BeamActive { get; set; }
	[Net] public Entity GrabbedEntity { get; set; }
	[Net] public int GrabbedBone { get; set; }
	[Net] public Vector3 GrabbedPos { get; set; }

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "weapons/rust_pistol/rust_pistol.vmdl" );
	}

	public void OnPlayerControlTick( Player owner )
	{
		if ( owner == null ) return;

		var input = owner.Input;
		var eyePos = owner.EyePos;
		var eyeDir = owner.EyeRot.Forward;
		var eyeRot = Rotation.From( new Angles( 0.0f, owner.EyeAng.yaw, 0.0f ) );

		if ( !grabbing && input.Pressed( InputButton.Attack1 ) )
		{
			grabbing = true;
		}

		bool grabEnabled = grabbing && input.Down( InputButton.Attack1 );
		bool wantsToFreeze = input.Pressed( InputButton.Attack2 );

		if ( IsClient && wantsToFreeze )
		{
			grabEnabled = false;
			grabbing = false;
		}

		BeamActive = grabEnabled;

		if ( IsServer )
		{
			if ( !holdBody.IsValid() )
				return;

			if ( grabEnabled )
			{
				if ( heldBody.IsValid() )
				{
					UpdateGrab( input, eyePos, eyeRot, eyeDir, wantsToFreeze );
				}
				else
				{
					TryStartGrab( owner, eyePos, eyeRot, eyeDir );
				}
			}
			else if ( grabbing )
			{
				GrabEnd();
			}
		}

		if ( BeamActive )
		{
			owner.Input.MouseWheel = 0;
		}
	}

	private void TryStartGrab( Player owner, Vector3 eyePos, Rotation eyeRot, Vector3 eyeDir )
	{
		var tr = Trace.Ray( eyePos, eyePos + eyeDir * MaxTargetDistance )
			.UseHitboxes()
			.Ignore( owner ) 
			.Run();  

		if ( !tr.Hit ) return;
		if ( !tr.Body.IsValid() ) return;
		if ( tr.Entity.IsWorld ) return;

		var body = tr.Body;

		if ( tr.Entity.Parent.IsValid() )
		{
			var rootEnt = tr.Entity.Root;

			if ( rootEnt.IsValid() && rootEnt.PhysicsGroup != null )
			{
				body = rootEnt.PhysicsGroup.GetBody(0);
			}
		}

		if ( !body.IsValid() )
			return;

		//
		// Don't move keyframed 
		//
		if ( body.BodyType == PhysicsBodyType.Keyframed )
			return;

		// Unfreeze
		if ( body.BodyType == PhysicsBodyType.Static )
		{
			body.BodyType = PhysicsBodyType.Dynamic;
		}

		GrabInit( body, eyePos, tr.EndPos, eyeRot );

		GrabbedEntity = tr.Entity.Root;
		GrabbedPos = body.Transform.PointToLocal( tr.EndPos );
		GrabbedBone = tr.Entity.PhysicsGroup.GetBodyIndex( body );
	}

	private void UpdateGrab( UserInput input, Vector3 eyePos, Rotation eyeRot, Vector3 eyeDir, bool wantsToFreeze )
	{
		if ( wantsToFreeze )
		{
			heldBody.BodyType = PhysicsBodyType.Static;

			if ( GrabbedEntity.IsValid() )
			{
				var freezeEffect = Particles.Create( "particles/physgun_freeze.vpcf" );
				freezeEffect.SetPos( 0, heldBody.Transform.PointToWorld( GrabbedPos ) );
			}

			GrabEnd();
			return;
		}

		MoveTargetDistance( input.MouseWheel * TargetDistanceSpeed );

		if ( input.Down( InputButton.Use ) )
		{
			EnableAngularSpring( true );
			DoRotate( eyeRot, input.MouseDelta * RotateSpeed );
		}
		else
		{
			EnableAngularSpring( false );
		}

		GrabMove( eyePos, eyeDir, eyeRot );
	}

	private void EnableAngularSpring( bool enabled )
	{
		if ( holdJoint.IsValid() )
		{
			holdJoint.AngularDampingRatio = enabled ? AngularDampingRatio : 0.0f;
			holdJoint.AngularFrequency = enabled ? AngularFrequency : 0.0f;
		}
	}

	private void Activate()
	{
		if ( !holdBody.IsValid() )
		{
			holdBody = PhysicsWorld.AddBody();
			holdBody.BodyType = PhysicsBodyType.Keyframed;
		}
	}

	private void Deactivate()
	{
		GrabEnd();

		holdBody?.Remove();
		holdBody = null;

		KillEffects();
	}

	public override void ActiveStart( Entity ent )
	{
		base.ActiveStart( ent );

		if ( IsServer )
		{
			Activate();
		}
	}

	public override void ActiveEnd( Entity ent, bool dropped )
	{
		base.ActiveEnd( ent, dropped );

		if ( IsServer )
		{
			Deactivate();
		}
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		if ( IsServer )
		{
			Deactivate();
		}
	}

	private void GrabInit( PhysicsBody body, Vector3 startPos, Vector3 grabPos, Rotation rot )
	{
		if ( !body.IsValid() )
			return;

		GrabEnd();

		grabbing = true;
		heldBody = body;
		holdDistance = Vector3.DistanceBetween( startPos, grabPos );
		holdDistance = holdDistance.Clamp( MinTargetDistance, MaxTargetDistance );
		heldPos = heldBody.Transform.PointToLocal( grabPos );
		heldRot = rot.Inverse * heldBody.Rot;

		holdBody.Pos = grabPos;
		holdBody.Rot = heldBody.Rot;

		heldBody.WakeUp();
		heldBody.EnableAutoSleeping = false;

		holdJoint = PhysicsJoint.Weld
			.From( holdBody )
			.To( heldBody, heldPos )
			.WithLinearSpring( LinearFrequency, LinearDampingRatio, 0.0f )
			.WithAngularSpring( 0.0f, 0.0f, 0.0f )
			.Create();
	}

	private void GrabEnd()
	{
		if ( holdJoint.IsValid() )
		{
			holdJoint.Remove();
		}

		if ( heldBody.IsValid() )
		{
			heldBody.EnableAutoSleeping = true;
		}

		heldBody = null;
		GrabbedEntity = null;
		grabbing = false;
	}

	private void GrabMove( Vector3 startPos, Vector3 dir, Rotation rot )
	{
		if ( !heldBody.IsValid() )
			return;

		holdBody.Pos = startPos + dir * holdDistance;
		holdBody.Rot = rot * heldRot;
	}

	private void MoveTargetDistance( float distance )
	{
		holdDistance += distance;
		holdDistance = holdDistance.Clamp( MinTargetDistance, MaxTargetDistance );
	}

	protected virtual void DoRotate( Rotation eye, Vector3 input )
	{
		var localRot = eye;
		localRot *= Rotation.FromAxis( Vector3.Up, input.x );
		localRot *= Rotation.FromAxis( Vector3.Right, input.y );
		localRot = eye.Inverse * localRot;

		heldRot = localRot * heldRot;
	}

	public void BuildInput( ClientInput owner )
	{
		if ( !GrabbedEntity.IsValid() )
			return;

		if ( !owner.Down( InputButton.Attack1 ) )
			return;

		if ( owner.Down( InputButton.Use ) )
		{
			owner.ViewAngles = owner.LastViewAngles;
		}
	}
}
