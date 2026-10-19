using System;
using System.Globalization;
using UnityEngine;

/// <summary>The saved account is independent of a scene, loadout, or failed run.</summary>
public static class FacilityEconomy
{
    public const int HeistPayout = 2500;
    public const int ShotgunPrice = 5000;
    public const int AK47Price = 10000;
    const string SaveKey = "Facility.HeistAccount.v1";

    static Account account;
    static Account Current => account ?? (account = new Account(SaveKey));
    public static int Cash => Current.Cash;
    public static bool OwnsPistol => true;
    public static bool OwnsShotgun => Current.Owns(FacilityWeaponKind.Shotgun);
    public static bool OwnsAK47 => Current.Owns(FacilityWeaponKind.AK47);
    public static string CashLabel => FormatCash(Cash);
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { account = null; Changed = null; }

    /// <summary>Start a fresh attempt. This never clears money or purchased weapons.</summary>
    public static void BeginRun() => Current.BeginRun();

    /// <summary>Call only after extraction with both cash and gold. Pays once per attempt.</summary>
    public static bool AwardHeist()
    {
        if (!Current.AwardHeist()) return false;
        Changed?.Invoke();
        return true;
    }

    public static bool Owns(FacilityWeaponKind kind) => Current.Owns(kind);
    public static int Price(FacilityWeaponKind kind) => kind == FacilityWeaponKind.Shotgun ? ShotgunPrice : kind == FacilityWeaponKind.AK47 ? AK47Price : 0;
    public static string FormatCash(int value) => "$" + value.ToString("N0", CultureInfo.InvariantCulture);

    public static bool TryPurchase(FacilityWeaponKind kind, out string reason)
    {
        if (!Current.TryPurchase(kind, out reason)) return false;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Reloads the current saved record, for a fresh process-style persistence check.</summary>
    public static void ReloadSavedAccount()
    {
        account = new Account(Current.Key);
        Changed?.Invoke();
    }

    /// <summary>A disposable diagnostic account that cannot change the player's saved wallet.</summary>
    public static IDisposable UseIsolatedAccount()
    {
        Account previous = Current;
        string temporaryKey = "Facility.HeistAccount.Diagnostic." + Guid.NewGuid().ToString("N");
        account = new Account(temporaryKey);
        Changed?.Invoke();
        return new IsolatedAccount(previous, temporaryKey);
    }

    sealed class IsolatedAccount : IDisposable
    {
        readonly Account previous;
        readonly string key;
        bool disposed;
        public IsolatedAccount(Account previousAccount, string temporaryKey) { previous = previousAccount; key = temporaryKey; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            account = previous;
            Changed?.Invoke();
        }
    }

    [Serializable]
    sealed class Profile
    {
        public int version = 1;
        public int cash;
        public bool shotgun;
        public bool ak47;
    }

    // A single saved record keeps a purchase's debit and ownership change together.
    sealed class Account
    {
        readonly string key;
        readonly Profile profile;
        bool runStarted, rewarded;
        public int Cash => profile.cash;
        public string Key => key;

        public Account(string saveKey)
        {
            key = saveKey;
            try
            {
                string saved = PlayerPrefs.GetString(key, string.Empty);
                profile = string.IsNullOrEmpty(saved) ? new Profile() : JsonUtility.FromJson<Profile>(saved);
            }
            catch (ArgumentException) { profile = new Profile(); }
            if (profile == null || profile.version != 1) profile = new Profile();
            profile.cash = Mathf.Max(0, profile.cash);
        }

        public bool Owns(FacilityWeaponKind kind) => kind == FacilityWeaponKind.Pistol || kind == FacilityWeaponKind.Fists ||
            (kind == FacilityWeaponKind.Shotgun && profile.shotgun) || (kind == FacilityWeaponKind.AK47 && profile.ak47);

        public void BeginRun() { runStarted = true; rewarded = false; }

        public bool AwardHeist()
        {
            if (!runStarted || rewarded) return false;
            rewarded = true;
            profile.cash = (int)Math.Min(int.MaxValue, (long)profile.cash + HeistPayout);
            Save();
            return true;
        }

        public bool TryPurchase(FacilityWeaponKind kind, out string reason)
        {
            if (kind != FacilityWeaponKind.Pistol && kind != FacilityWeaponKind.Shotgun && kind != FacilityWeaponKind.AK47)
            { reason = "That item is unavailable."; return false; }
            if (Owns(kind)) { reason = "Already owned. Ready in the car."; return false; }
            int price = Price(kind);
            if (profile.cash < price)
            { reason = "You need " + FormatCash(price - profile.cash) + " more."; return false; }
            profile.cash -= price;
            if (kind == FacilityWeaponKind.Shotgun) profile.shotgun = true;
            else if (kind == FacilityWeaponKind.AK47) profile.ak47 = true;
            Save();
            reason = "Purchase saved. Ready in the car.";
            return true;
        }

        void Save()
        {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }
    }

#if UNITY_EDITOR
    /// <summary>Validates the real save path using a disposable account, never the player's key.</summary>
    public static string ValidateIsolatedAccount()
    {
        string key = "Facility.HeistAccount.Validation." + Guid.NewGuid().ToString("N");
        int checks = 0;
        Action<bool, string> check = (condition, message) =>
        { if (!condition) throw new InvalidOperationException("Economy validation: " + message); checks++; };
        try
        {
            var test = new Account(key);
            check(test.Cash == 0 && test.Owns(FacilityWeaponKind.Pistol), "Fresh account/pistol ownership");
            check(!test.AwardHeist(), "No award before a run");
            check(!test.TryPurchase(FacilityWeaponKind.Shotgun, out _) && test.Cash == 0, "No overspending");
            test.BeginRun();
            check(test.AwardHeist() && test.Cash == HeistPayout, "Successful extraction payout");
            check(!test.AwardHeist() && test.Cash == HeistPayout, "Duplicate extraction guard");
            test = new Account(key);
            check(test.Cash == HeistPayout, "Cash persists after account reload");
            test.BeginRun(); test.AwardHeist();
            check(test.TryPurchase(FacilityWeaponKind.Shotgun, out _) && test.Cash == 0, "Exact-price shotgun purchase");
            test = new Account(key);
            check(test.Owns(FacilityWeaponKind.Shotgun) && test.Cash == 0, "Purchase and debit persist together");
            check(!test.TryPurchase(FacilityWeaponKind.Shotgun, out _) && test.Cash == 0, "Owned weapon cannot charge again");
            for (int i = 0; i < 4; i++) { test.BeginRun(); test.AwardHeist(); }
            check(test.TryPurchase(FacilityWeaponKind.AK47, out _) && test.Cash == 0, "AK-47 exact-price purchase");
            test = new Account(key);
            check(test.Owns(FacilityWeaponKind.AK47) && test.Owns(FacilityWeaponKind.Shotgun) && test.Owns(FacilityWeaponKind.Pistol), "All ownership survives reload");
            test.BeginRun();
            test = new Account(key);
            check(test.Cash == 0 && test.Owns(FacilityWeaponKind.AK47), "Unpaid/failed attempt preserves account");
            return checks + " economy checks passed; disposable save removed.";
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
    }
#endif
}
