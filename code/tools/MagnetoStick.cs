namespace Sandbox.Tools
{
	[ClassLibrary( "tool_magnetostick", Title = "MagnetoStick", Group = "construction" )]
	public partial class MagnetoTool : BaseTool
	{
		private Prop target;

		public override void OnPlayerControlTick()
		{
			if ( !Host.IsServer )
				return;

			using ( Prediction.Off() )
			{
				test
				// I might not want to overwrite super's class memeber MaxTraceDistance
				// ~56.54 hu to m based on rough doorway estimates
				traceDistance = 75 //hu | ? // TODO: To be adjusted when tested 
				var input = Owner.Input;

				var startPos = Owner.EyePos;
				var dir = Owner.EyeRot.Forward;

				var tr = Trace.Ray( startPos, startPos + dir * MaxTraceDistance )
					.Ignore( Owner )
					.Run();

				if ( !tr.Hit )
					return;

				if ( !tr.Entity.IsValid() )
					return;

				if ( tr.Entity.IsWorld ) 
				{
					if ( this.attached )
					{
						// Spawn rope of N lengh (32hu?) to connect points of attachment
						{}
					}
					return;
				}

				if ( tr.Entity == target )  // Not sure what a target is
					return;

				if ( !tr.Body.IsValid() ) 
					return;

				if ( tr.Entity.PhysicsGroup == null || tr.Entity.PhysicsGroup.BodyCount > 1 )
					return;

				if ( tr.Entity is not Prop prop ) // Do Ragdolls count as props?
					return;

				if ( !target.IsValid() )
				{
					target = prop;
				}

				else
				{
					if ( !input.Pressed( InputButton.Attack1 ) {
						// Hold Left Click to engage magnet affect. moving all items near/directional away with a small force
						{}
					};
					if ( !input.Pressed( InputButton.Attack2 ) {
						/** this.attached:False -- Right Click to grab object. Take first object on crosshair of certain distance away to hold in front of magnetostick (ragdolled if possible) 
							this.attached:True -- Right Click to disattach object: */

						// Toggle attached mode
						{};
					};
					// target is not prop and valid
					// implement shove function here
					target = null
				}

			}
		}
	}
}