using UnityEngine;

namespace Common.Username
{
    public sealed class UsernameRepository
    {
        private const string SaveKey = "Username";

        public void Save(string username)
        {
            PlayerPrefs.SetString(SaveKey, username);
            PlayerPrefs.Save();
        }

        public string Load()
        {
            string value = PlayerPrefs.GetString(SaveKey, string.Empty);
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
