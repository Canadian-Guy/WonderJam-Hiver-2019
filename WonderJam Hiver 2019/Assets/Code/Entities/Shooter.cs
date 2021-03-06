﻿using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Shooter : MonoBehaviour 
{
	[Tooltip("The pattern shot by this shooter")]
	public ShotPattern m_patternToShoot;

	[Tooltip("The minimum delay between pattern starts/loops")]
	[Range(0, 2)] public float m_patternCooldown;
	private float m_lastShot;

	[Tooltip("Event called when the entity shoots.")]
	public UnityEvent m_shotEvent;

    [HideInInspector] public LinkedList<ShotPatternPowerUpWrapper> m_spPowerUps;
	[HideInInspector] public Entity m_entity;
	private Dictionary<ShotPattern, DataHolder> m_patterns;

	public void Init(Entity p_entity) 
	{
		m_entity = p_entity;
		m_patterns = new Dictionary<ShotPattern, DataHolder>();
        m_spPowerUps = new LinkedList<ShotPatternPowerUpWrapper>();
	}

	public bool CanShoot(ShotPattern p_pattern)
	{
		if(Time.timeScale == 0f) return false;

		object active = GetPatternInfo(p_pattern, "active");
		if(m_patterns.ContainsKey(p_pattern) && active != null && (bool) active) return false;
		if(m_patternCooldown == 0) return true;
		if(!CanLoop(p_pattern)) return false;
		if(p_pattern.m_bypassShooterCooldown) return true;

		return Time.time * 1000 >= m_lastShot + m_patternCooldown * 1000;
	}

	public bool CanLoop(ShotPattern p_pattern) 
	{
		object lastLoopTimeObj = GetPatternInfo(p_pattern, "lastLoopTime");
		float patternCooldown = p_pattern.m_patternCooldown;

		return lastLoopTimeObj == null ? true : Time.time * 1000 >= (float) lastLoopTimeObj + patternCooldown * 1000;
	}

    public ShotPattern GetCurrentPattern()
    {
        if(m_spPowerUps.Count == 0)
        {
            return m_patternToShoot;
        }
        else
        {
            return m_spPowerUps.Last.Value.shotPatternPowerUp.m_powerUpPattern;
        }
    }

    public void AddShotPatternPowerUp(ShotPatternPowerUp p_shotPatternPowerUp)
    {
        if(m_spPowerUps.Any(s => s.shotPatternPowerUp == p_shotPatternPowerUp))
        {
            ShotPatternPowerUpWrapper wrapper = m_spPowerUps.Last(s => s.shotPatternPowerUp == p_shotPatternPowerUp);
            wrapper.time = Time.time * 1000;
            m_spPowerUps.Remove(wrapper);
            m_spPowerUps.AddLast(wrapper);
        }
        else
        {
            ShotPatternPowerUpWrapper wrapper = new ShotPatternPowerUpWrapper(p_shotPatternPowerUp);
            m_spPowerUps.AddLast(wrapper);
            StartCoroutine(RemoveShotPatternPowerUp(wrapper, p_shotPatternPowerUp.m_duration));
        }   
    }

    IEnumerator RemoveShotPatternPowerUp(ShotPatternPowerUpWrapper p_shotPatternPowerUp, float p_duration)
    {
        yield return new WaitForSeconds(p_duration);
        if(Time.time * 1000 < p_shotPatternPowerUp.time + p_shotPatternPowerUp.shotPatternPowerUp.m_duration * 1000)
        {
            StartCoroutine(RemoveShotPatternPowerUp(p_shotPatternPowerUp, (p_shotPatternPowerUp.time + p_shotPatternPowerUp.shotPatternPowerUp.m_duration * 1000 - Time.time * 1000)/1000));
        }
        else
        {
            m_spPowerUps.Remove(p_shotPatternPowerUp);
            p_shotPatternPowerUp.shotPatternPowerUp.End(this);
        }
    }

	public void Shoot(ShotPattern p_pattern) 
	{
		if(!CanShoot(p_pattern)) return;

		m_lastShot = Time.time * 1000;

		if(!m_patterns.ContainsKey(p_pattern)) m_patterns.Add(p_pattern, new DataHolder());

		SetPatternInfo(p_pattern, "shotsFired", 0);
		SetPatternInfo(p_pattern, "loops", 0);
		p_pattern.Init(this);
		SetPatternInfo(p_pattern, "active", true);

		StartCoroutine(PatternStep(p_pattern));
	}

	private IEnumerator PatternStep(ShotPattern p_pattern) 
	{
		while((bool) GetPatternInfo(p_pattern, "active"))
		{ 
			float delay = p_pattern.m_stepDelay;

			if(p_pattern.m_instant) delay = p_pattern.Instant(this);
			else delay = p_pattern.PreStep(this);

			if(delay == -1) delay = p_pattern.m_stepDelay;

			yield return new WaitForSeconds(delay);
		}
	}

	public object GetPatternInfo(ShotPattern p_pattern, string p_key) 
	{
		DataHolder data = null;

		bool success = m_patterns.TryGetValue(p_pattern, out data);

		return success ? data.Get(p_key) : null;
	}

	public void SetPatternInfo(ShotPattern p_pattern, string p_key, object p_value)
	{ 
		if(!m_patterns.ContainsKey(p_pattern)) m_patterns.Add(p_pattern, new DataHolder());

		m_patterns[p_pattern].Set(p_key, p_value);
	}

	public void StopShooting() 
	{
		if(m_patterns.Count > 0)
			foreach(ShotPattern pattern in m_patterns.Keys)
				StopShooting(pattern);
	}

	public void StopShooting(ShotPattern p_pattern)
	{
		SetPatternInfo(p_pattern, "active", false);

		if(p_pattern.m_nextPatterns.Count > 0) StartCoroutine(Transition(p_pattern));
	}

	private IEnumerator Transition(ShotPattern p_pattern) 
	{ 
		yield return new WaitForSeconds(p_pattern.m_nextPatternSwitchDelay);

		p_pattern.Transition(this);
	}

	// this is the pure event, with no modifications applied prior
	public void Damage(Projectile p_projectile, Entity p_entity) 
	{
		int finalDamage = p_projectile.m_info.m_damage;

		p_entity.Damage(m_entity, finalDamage, false);
	}
}

public class ShotPatternPowerUpWrapper
{
    public ShotPatternPowerUp shotPatternPowerUp;
    public float time;

    public ShotPatternPowerUpWrapper(ShotPatternPowerUp p_shotPatternPowerUp)
    {
        shotPatternPowerUp = p_shotPatternPowerUp;
        time = Time.time * 1000;
    }
}
