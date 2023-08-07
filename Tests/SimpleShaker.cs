using Godot;

namespace SadChromaLib.Specialisations.Dialogue.Tests;

public sealed partial class SimpleShaker: RefCounted
{
	private const float Intensity = 8.0f;

	private Node2D _targetRef;
	private float _fac;
	private Vector2 _originalOffset;

	public SimpleShaker(Node2D target)
	{
		_targetRef = target;
		_fac = 0.0f;

		_originalOffset = _targetRef.Position;
	}

	public void Shake()
	{
		_fac = 1.0f;
	}

	public void Exec()
	{
		_fac = Mathf.Lerp(_fac, 0.0f, 0.1f);

		Vector2 randomDir = new(
			(float) GD.RandRange(-1.0, 1.0),
			(float) GD.RandRange(-1.0, 1.0)
		);

		Vector2 newPosition = _originalOffset + (randomDir * Intensity);
		_targetRef.Position = _targetRef.Position.Lerp(newPosition, _fac);
	}
}