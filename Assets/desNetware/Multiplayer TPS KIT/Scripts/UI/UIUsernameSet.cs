using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
namespace MTPSKIT
{
    public class UIUsernameSet : MonoBehaviour
    {
        [SerializeField] InputField UsernameField;

        string _playerPrefs_Username;

        private void Start()
        {
            UsernameField.onEndEdit.AddListener(UsernameModified);

            ReadUsernameFromPlayerPrefs();

        }

        void ReadUsernameFromPlayerPrefs()
        {
            string username = PlayerPrefs.GetString(_playerPrefs_Username);
            UsernameField.text = username;
            UserSettings.UserNickname = username;
        }



        void UsernameModified(string s)
        {
            UserSettings.UserNickname = CheckUsername(UsernameField.text);
            UsernameField.text = UserSettings.UserNickname;

            PlayerPrefs.SetString(_playerPrefs_Username, UserSettings.UserNickname);
        }

        string CheckUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return "Guest";
            }

            return username;
        }
    }
}