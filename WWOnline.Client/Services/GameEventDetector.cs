using Serilog;
using WWOnline.Shared.Models;

namespace WWOnline.Services;

/// <summary>
/// Detects game events by monitoring memory changes
/// </summary>
public class GameEventDetector
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<GameEventDetector>();

    // Event delegates
    public delegate Task OnRupeeCollectedDelegate(ushort previousAmount, ushort newAmount, int change);
    public delegate Task OnHealthChangedDelegate(ushort previousHealth, ushort newHealth, int change);
    public delegate Task OnItemUsedDelegate(string itemType, byte previousAmount, byte newAmount);
    public delegate Task OnItemCollectedDelegate(string itemType, byte amount);
    
    // Event handlers
    public event OnRupeeCollectedDelegate? OnRupeeCollected;
    public event OnHealthChangedDelegate? OnHealthChanged;
    public event OnItemUsedDelegate? OnItemUsed;
    public event OnItemCollectedDelegate? OnItemCollected;
    
    /// <summary>
    /// Check for events based on game state changes
    /// </summary>
    public async Task DetectEvents(GameState currentState, GameState? previousState)
    {
        if (previousState == null)
            return;
        
        // Check for rupee changes
        if (currentState.Player.RupeeCount != previousState.Player.RupeeCount)
        {
            int change = currentState.Player.RupeeCount - previousState.Player.RupeeCount;
            
            if (change > 0)
            {
                Logger.Information("🪙 Rupee collected! {Previous} -> {New} (+{Change})", 
                    previousState.Player.RupeeCount, currentState.Player.RupeeCount, change);
                
                // Determine rupee type based on amount
                string rupeeType = change switch
                {
                    1 => "Green Rupee",
                    5 => "Blue Rupee",
                    10 => "Yellow Rupee",
                    20 => "Red Rupee",
                    50 => "Purple Rupee",
                    100 => "Orange Rupee",
                    200 => "Silver Rupee",
                    _ => $"Rupees x{change}"
                };
                
                Logger.Information("Collected: {RupeeType}", rupeeType);
                
                if (OnRupeeCollected != null)
                {
                    await OnRupeeCollected(previousState.Player.RupeeCount, 
                        currentState.Player.RupeeCount, change);
                }
            }
            else if (change < 0)
            {
                Logger.Information("💸 Rupees spent: {Previous} -> {New} ({Change})", 
                    previousState.Player.RupeeCount, currentState.Player.RupeeCount, change);
            }
        }
        
        // Check for health changes
        if (currentState.Player.CurrentHealth != previousState.Player.CurrentHealth)
        {
            int change = currentState.Player.CurrentHealth - previousState.Player.CurrentHealth;
            
            if (change > 0)
            {
                Logger.Information("❤️ Health restored: {Previous} -> {New} (+{Change})", 
                    previousState.Player.CurrentHealth, currentState.Player.CurrentHealth, change);
            }
            else
            {
                Logger.Information("💔 Damage taken: {Previous} -> {New} ({Change})", 
                    previousState.Player.CurrentHealth, currentState.Player.CurrentHealth, change);
            }
            
            if (OnHealthChanged != null)
            {
                await OnHealthChanged(previousState.Player.CurrentHealth, 
                    currentState.Player.CurrentHealth, change);
            }
        }
        
        // Check for arrow usage/collection
        if (currentState.Player.CurrentArrows != previousState.Player.CurrentArrows)
        {
            int change = currentState.Player.CurrentArrows - previousState.Player.CurrentArrows;
            
            if (change > 0)
            {
                Logger.Information("🏹 Arrows collected: {Previous} -> {New} (+{Change})", 
                    previousState.Player.CurrentArrows, currentState.Player.CurrentArrows, change);
                
                if (OnItemCollected != null)
                {
                    await OnItemCollected("Arrows", (byte)change);
                }
            }
            else
            {
                Logger.Debug("Arrow used: {Previous} -> {New}", 
                    previousState.Player.CurrentArrows, currentState.Player.CurrentArrows);
                
                if (OnItemUsed != null)
                {
                    await OnItemUsed("Arrows", previousState.Player.CurrentArrows, 
                        currentState.Player.CurrentArrows);
                }
            }
        }
        
        // Check for bomb usage/collection
        if (currentState.Player.CurrentBombs != previousState.Player.CurrentBombs)
        {
            int change = currentState.Player.CurrentBombs - previousState.Player.CurrentBombs;
            
            if (change > 0)
            {
                Logger.Information("💣 Bombs collected: {Previous} -> {New} (+{Change})", 
                    previousState.Player.CurrentBombs, currentState.Player.CurrentBombs, change);
                
                if (OnItemCollected != null)
                {
                    await OnItemCollected("Bombs", (byte)change);
                }
            }
            else
            {
                Logger.Debug("Bomb used: {Previous} -> {New}", 
                    previousState.Player.CurrentBombs, currentState.Player.CurrentBombs);
                
                if (OnItemUsed != null)
                {
                    await OnItemUsed("Bombs", previousState.Player.CurrentBombs, 
                        currentState.Player.CurrentBombs);
                }
            }
        }
        
        // Check for magic usage/restoration
        if (currentState.Player.CurrentMagic != previousState.Player.CurrentMagic)
        {
            int change = currentState.Player.CurrentMagic - previousState.Player.CurrentMagic;
            
            if (change > 0)
            {
                Logger.Information("✨ Magic restored: {Previous} -> {New} (+{Change})", 
                    previousState.Player.CurrentMagic, currentState.Player.CurrentMagic, change);
            }
            else
            {
                Logger.Debug("Magic used: {Previous} -> {New}", 
                    previousState.Player.CurrentMagic, currentState.Player.CurrentMagic);
            }
        }
        
        // Check for equipment changes
        if (currentState.Player.CurrentSword != previousState.Player.CurrentSword)
        {
            string swordName = GetSwordName(currentState.Player.CurrentSword);
            string previousSword = GetSwordName(previousState.Player.CurrentSword);
            Logger.Information("⚔️ Sword changed: {Previous} -> {New}", previousSword, swordName);
        }
        
        if (currentState.Player.CurrentShield != previousState.Player.CurrentShield)
        {
            string shieldName = GetShieldName(currentState.Player.CurrentShield);
            string previousShield = GetShieldName(previousState.Player.CurrentShield);
            Logger.Information("🛡️ Shield changed: {Previous} -> {New}", previousShield, shieldName);
        }
        
    }

    private string GetSwordName(byte swordId)
    {
        return swordId switch
        {
            0 => "None",
            1 => "Hero's Sword",
            2 => "Master Sword",
            3 => "Master Sword (Half Power)",
            4 => "Master Sword (Full Power)",
            _ => $"Unknown ({swordId})"
        };
    }
    
    private string GetShieldName(byte shieldId)
    {
        return shieldId switch
        {
            0 => "None",
            1 => "Hero's Shield",
            2 => "Mirror Shield",
            _ => $"Unknown ({shieldId})"
        };
    }
}