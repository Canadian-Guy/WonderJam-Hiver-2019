﻿using UnityEngine;
using System.Collections.Generic;

public abstract class ShotPattern : ScriptableObject 
{
	[Header("Generic Attributes")]
	[Tooltip("Projectile to shoot in this pattern")]
	public Projectile m_projectile;

	[Tooltip("The information to attribute to every projectile in the pattern")]
	public ProjectileInfo m_projectileInfo;

	[Tooltip("Behaviours to add to every shot projectile in the pattern")]
	public List<ProjectileBehaviour> m_behaviours;

	[Tooltip("Amount of shots to include in this pattern")]
	[Range(1, 1000)] public int m_shots;

	[Tooltip("If the shot happens instantly")]
	public bool m_instant;
	
	[Tooltip("If the pattern continues after the first loop")]
	public bool m_loop;

	[Tooltip("How many loops of the pattern will be shot before the pattern stops/transitions, 0 = infinite")]
	[ConditionalField("m_loop")] public int m_loopsBeforeSwitch;

	[Tooltip("Time in seconds to wait between pattern loops/restarts")]
	[Range(0, 10)] public float m_patternCooldown;

	[Tooltip("Should this shot pattern be allowed to shoot regardless of the shooter's shot cooldown?")]
	public bool m_bypassShooterCooldown;

	[Tooltip("Time in seconds before the next pattern update (next shot in the pattern)")]
	[Range(0, 10)] public float m_stepDelay;

	[Tooltip("Patterns to switch to after this one ends (they overlap), can be null")]
	public List<ShotPattern> m_nextPatterns;

	[Tooltip("The delay in seconds before this pattern switches to the next one")]
	[Range(0, 10)] public float m_nextPatternSwitchDelay;
    
    [Tooltip("Flip the origin of the shooting location")]
    public bool m_flipShootOrigin;

    protected Vector2 FetchTarget(Shooter p_shooter, Projectile p_projectile) 
	{
		object forcedTarget = p_shooter.GetPatternInfo(this, "forcedTarget");

		return forcedTarget == null || (Vector2) forcedTarget == Vector2.zero ? (Vector2) p_projectile.transform.up : (Vector2) forcedTarget;
	}

	protected Projectile SpawnProjectile(Shooter p_shooter) 
	{
		GameObject proj = Game.m_projPool.Get();
		Projectile projectile = proj.GetComponent<Projectile>();
		object spawnLocation = p_shooter.GetPatternInfo(this, "spawnLocation");
        float offset = p_shooter.m_entity.transform.localScale.y / 2; //m_entity.m_renderer.sprite.bounds.max.y;

        proj.transform.position = spawnLocation == null ? p_shooter.transform.position : (Vector3) spawnLocation;
        proj.transform.position += new Vector3(0, m_flipShootOrigin ? -offset : offset, 0);
		proj.transform.rotation = m_projectile.transform.rotation;

		projectile.Clone(m_projectile, m_projectileInfo, m_behaviours);

		return projectile;
	}

	public void Transition(Shooter p_shooter) 
	{
		for(int i = 0; i < m_nextPatterns.Count; ++i)
			p_shooter.Shoot(m_nextPatterns[i]);
	}

	public float Instant(Shooter p_shooter) 
	{
		for(int i = 0; i < m_shots; ++i) PreStep(p_shooter);
		if((int) p_shooter.GetPatternInfo(this, "shotsFired") == m_shots) AddLoop(p_shooter);
		if(!p_shooter.CanLoop(this)) return (Time.time * 1000 - ((float) p_shooter.GetPatternInfo(this, "lastLoopTime") + m_patternCooldown * 1000)) / 1000;

		return m_patternCooldown;
	}

	public float PreStep(Shooter p_shooter) 
	{
		if(!((bool) p_shooter.GetPatternInfo(this, "active"))) return -1;

		int shotsFired = (int) p_shooter.GetPatternInfo(this, "shotsFired");

		if(shotsFired == m_shots)
			if(AddLoop(p_shooter)) return -1;

		if(!p_shooter.CanLoop(this)) return (Time.time * 1000 - ((float) p_shooter.GetPatternInfo(this, "lastLoopTime") + m_patternCooldown * 1000)) / 1000;

		Step(p_shooter);

		shotsFired++;
		p_shooter.SetPatternInfo(this, "shotsFired", shotsFired);

		return m_stepDelay;
	}

	private bool AddLoop(Shooter p_shooter) 
	{
		p_shooter.SetPatternInfo(this, "shotsFired", 0);
		p_shooter.SetPatternInfo(this, "loops", (int) p_shooter.GetPatternInfo(this, "loops") + 1);
		p_shooter.SetPatternInfo(this, "lastLoopTime", Time.time * 1000);

		if(IsDoneLooping(p_shooter))
		{
			p_shooter.StopShooting(this);
			return true;
		}

		Init(p_shooter);

		return false;
	}

	private bool IsDoneLooping(Shooter p_shooter) 
	{
		int loops = (int) p_shooter.GetPatternInfo(this, "loops");

		return (m_loop && loops >= m_loopsBeforeSwitch && m_loopsBeforeSwitch != 0) || (!m_loop && loops >= 1);
	}

	public abstract void Init(Shooter p_shooter);

	public abstract void Step(Shooter p_shooter);
}
