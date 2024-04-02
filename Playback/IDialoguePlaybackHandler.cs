using System;
using SadChromaLib.Specialisations.Dialogue.Types;

namespace SadChromaLib.Specialisations.Dialogue.Playback;

/// <summary>
/// An interface implementing handlers to dialogue playback events
/// </summary>
public interface IDialoguePlaybackHandler
{
    public void OnPlaybackPresentDialogue(string character, string dialogue);
	public void OnPlaybackPresentChoices(DialogueChoice[] choices);
	public void OnPlaybackEvaluateCommand(DialoguePlayback playbackRef, DialogueCommand command);
	public void OnPlaybackCompleted(DialoguePlayback playbackRef);
}