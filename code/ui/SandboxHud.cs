﻿using Sandbox;
using Sandbox.UI;

[Library]
public partial class SandboxHud : Hud
{
	public SandboxHud()
	{
		if ( !IsClient )
			return;

		RootPanel.StyleSheet.Load( "/ui/SandboxHud.scss" );

		RootPanel.AddChild<NameTags>();
		RootPanel.AddChild<CrosshairCanvas>();
		RootPanel.AddChild<ChatBox>();
		RootPanel.AddChild<VoiceList>();
		RootPanel.AddChild<KillFeed>();
		RootPanel.AddChild<Scoreboard<ScoreboardEntry>>();
		RootPanel.AddChild<Health>();
		RootPanel.AddChild<InventoryBar>();
		RootPanel.AddChild<SpawnMenu>();
	}
}
