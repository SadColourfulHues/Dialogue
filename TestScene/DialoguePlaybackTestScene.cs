using Godot;

using SadChromaLib.Dialogue;
using SadChromaLib.Dialogue.Nodes;

namespace Game.Tests;

public partial class DialoguePlaybackTestScene : Node2D
{
	private DialoguePlayback _playbackRef;

	private Panel _dialogueChrome;
	private Label _characterLabel;
	private Label _dialogueLabel;

	private Sprite2D _bodyARef;
	private Sprite2D _bodyBRef;

	private Control _choicesRef;

	private DialogueChoice[] _currentChoices;

	private SimpleShaker _shakerA;
	private SimpleShaker _shakerB;

	public override void _Ready()
	{
		_playbackRef = GetNode<DialoguePlayback>("%Playback");
		_playbackRef.SetVariable("playerName", System.Environment.UserName);

		_dialogueChrome = GetNode<Panel>("%Panel");
		_characterLabel = GetNode<Label>("%Character");
		_dialogueLabel = GetNode<Label>("%Dialogue");

		_choicesRef = GetNode<Control>("%Choices");

		_bodyARef = GetNode<Sprite2D>("%a");
		_bodyBRef = GetNode<Sprite2D>("%b");

		_shakerA = new(_bodyARef);
		_shakerB = new(_bodyBRef);

		_playbackRef.CharacterUpdated += OnUpdateCharacterId;
		_playbackRef.DialogueTextUpdated += OnUpdateDialogueText;
		_playbackRef.HandleCommandRequest += OnHandleCommand;
		_playbackRef.ChoicesUpdated += OnChoiceUpdate;
		_playbackRef.PlaybackCompleted += CloseDialogueChrome;

		_playbackRef.Next();

		for (int i = 0, l = _choicesRef.GetChildCount(); i < l; ++ i) {
			Button choiceButton = _choicesRef.GetChild<Button>(i);
			choiceButton.Pressed += () => OnChoiceSelected(choiceButton);
		}
	}

	public override void _Process(double delta)
	{
		_shakerA.Exec();
		_shakerB.Exec();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey)
			return;

		if (Input.IsActionJustPressed("ui_accept")) {
			_playbackRef.Next();
		}
	}

	#region Events

	private void OnHandleCommand(DialogueNodeCommand command)
	{
		if (command.Name == "add_box") {
			HandleAddBox();
		}
		else if (command.Name == "walk_away") {
			HandleWalkAway();
		}
		else if (command.Name == "shake") {
			HandleShake(command.Parameter == "a");
		}
	}

	private void OnUpdateCharacterId(StringName id)
	{
		_characterLabel.Text = id;
	}

	private void OnUpdateDialogueText(string text)
	{
		_dialogueLabel.Text = text;
	}

	private void OnChoiceUpdate(DialogueChoice[] choices)
	{
		_choicesRef.Visible = true;
		_currentChoices = choices;

		for (int i = 0; i < 4; ++ i) {
			Button choiceButton = _choicesRef.GetChild<Button>(i);

			if (i >= choices.Length) {
				choiceButton.Visible = false;
				continue;
			}

			choiceButton.Visible = true;
			choiceButton.Text = choices[i].ChoiceText;
		}
	}

	private void OnChoiceSelected(Button choiceButton)
	{
		DialogueChoice choice = _currentChoices[choiceButton.GetIndex()];
		_currentChoices = null;

		_playbackRef.Jump(choice);
	}

	private async void CloseDialogueChrome()
	{
		_dialogueChrome.Visible = false;
		SetProcessUnhandledInput(false);

		Timer closeTimer = new();
		AddChild(closeTimer);

		closeTimer.Start(2.0f);

		await ToSignal(closeTimer, Timer.SignalName.Timeout);

		GD.Print("Done!");
		GetTree().Quit();
	}

	#endregion

	#region Command Handlers

	private async void HandleAddBox()
	{
		if (_bodyBRef.GetChildCount() < 1)
			return;

		Node2D box = (Node2D) _bodyBRef.GetChildren()[0];

		Vector2 position = box.GlobalPosition;
		Vector2 oldPosition = position;

		float offset = _bodyBRef.GlobalPosition.X - position.X;

		box.Reparent(_bodyARef);
		position.X = _bodyARef.GlobalPosition.X + offset;
		position.Y -= 50.0f * box.GetIndex();

		CreateTween()
			.TweenProperty(box, "global_position", position, 0.5f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Bounce);

		SceneTree tree = GetTree();

		for (int i = 0, l = _bodyBRef.GetChildCount(); i < l; ++ i) {
			Vector2 fallPosition = oldPosition + (Vector2.Up * 50.0f * i);

			CreateTween()
				.TweenProperty(_bodyBRef.GetChild(i), "global_position", fallPosition, 0.5f)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Bounce);

			for (int delay = 0; delay < 5; ++ delay) {
				await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
		}
	}

	private void HandleWalkAway()
	{
		_bodyBRef.FlipH = true;

		CreateTween()
			.TweenProperty(_bodyBRef, "global_position", Vector2.Right * 480f, 0.8f)
			.SetEase(Tween.EaseType.In)
			.SetTrans(Tween.TransitionType.Quint);
	}

	private void HandleShake(bool a)
	{
		if (a) {
			_shakerA.Shake();
		}
		else {
			_shakerB.Shake();
		}
	}

	#endregion
}
