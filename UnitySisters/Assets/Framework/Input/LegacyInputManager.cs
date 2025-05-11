using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityFramework.Singleton.UnSafe;

namespace UnityFramework.InputSystem
{
#if ENABLE_LEGACY_INPUT_MANAGER
    public class LegacyInputManager : MonoSingleton<LegacyInputManager>
	{
		public class KeyInput
		{ 
			public KeyCode keyCode;
			public System.Action keyAction;
            public int actionCount = 0;
		}

		List<KeyInput> keyInputs = new List<KeyInput>();
		int count = 0;


        public System.Action anyKeyDownAction = null;   

        private void Update()
        {
            if (Input.anyKeyDown)
                anyKeyDownAction?.Invoke();


            for (int i = 0; i < count; i++)
			{
				KeyInput input = keyInputs[i];
				if (Input.GetKeyDown(input.keyCode))
				{
					input.keyAction();
                }

            }
        }

		public void AddListener(KeyCode keyCode, System.Action keyAction)
		{
            KeyInput keyInput = keyInputs.Find(x => x.keyCode == keyCode);
			if (keyInput == null)
			{
				keyInput = new KeyInput();
                keyInput.keyCode = keyCode;
                keyInputs.Add(keyInput);
            }

            keyInput.keyAction += keyAction;
            keyInput.actionCount++; 

            count = keyInputs.Count;

        }

        public void RemoveListener(KeyCode keyCode, System.Action keyAction)
        {
            KeyInput keyInput = keyInputs.Find(x => x.keyCode == keyCode);
            if (keyInput == null)
            {
                return;
            }

            keyInput.keyAction -= keyAction;
            keyInput.actionCount--;

            if (keyInput.actionCount < 1)
                keyInputs.Remove(keyInput);

            count = keyInputs.Count;

        }

    }
#endif
}
