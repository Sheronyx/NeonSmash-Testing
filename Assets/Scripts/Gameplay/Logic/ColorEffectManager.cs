using UnityEngine;

public class ColorEffectManager : MonoBehaviour
{
    public static ColorEffectManager Instance { get; private set; }

    private bool  _pinkActive;
    private float _pinkTimeRemaining;
    private float _multiplier = 1f;

    private bool  _blueActive;
    private float _blueTimeRemaining;
    private float _reactionTimeBonus;
    private float _blueDecayTimer;

    public float Multiplier        => _pinkActive ? _multiplier : 1f;
    public float ReactionTimeBonus => _blueActive ? _reactionTimeBonus : 0f;
    public float PinkTimeRemaining => _pinkTimeRemaining;
    public float BlueTimeRemaining => _blueTimeRemaining;
    public bool  PinkActive        => _pinkActive;
    public bool  BlueActive        => _blueActive;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        if (_pinkActive)
        {
            _multiplier        *= (1f - 0.01f * dt);
            _pinkTimeRemaining -= dt;
            if (_pinkTimeRemaining <= 0f)
            {
                _pinkActive = false;
                _multiplier = 1f;
            }
        }

        if (_blueActive)
        {
            _blueTimeRemaining -= dt;
            _blueDecayTimer    -= dt;

            if (_blueDecayTimer <= 0f)
            {
                _reactionTimeBonus = Mathf.Max(0f, _reactionTimeBonus - 0.1f);
                _blueDecayTimer    = 5f;
            }

            if (_blueTimeRemaining <= 0f)
            {
                _blueActive        = false;
                _reactionTimeBonus = 0f;
            }
        }
    }

    public void OnPinkHit()
    {
        if (!_pinkActive)
        {
            _pinkActive        = true;
            _pinkTimeRemaining = 10f;
        }
        _multiplier *= 1.1f;
    }

    public void OnBlueHit()
    {
        if (!_blueActive)
        {
            _blueActive        = true;
            _blueTimeRemaining = 10f;
            _blueDecayTimer    = 5f;
        }
        _reactionTimeBonus += 0.1f;
    }

    public void OnGreenHit()
    {
        if (_pinkActive) _pinkTimeRemaining += 2f;
        if (_blueActive) _blueTimeRemaining += 2f;
    }

    public void OnOrangeHit()
    {
        ResetEffects();
    }

    public void ResetEffects()
    {
        _pinkActive        = false;
        _blueActive        = false;
        _multiplier        = 1f;
        _reactionTimeBonus = 0f;
        _pinkTimeRemaining = 0f;
        _blueTimeRemaining = 0f;
        _blueDecayTimer    = 0f;
    }
}
