using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public class AddressableBuildLabels : ScriptableObject
{
    public const string NAME = "AddressableBuildLabels";

    [SerializeField] List<string> autoLoadLabels = new List<string>();
    [SerializeField] List<string> ignoreLabels = new List<string>();    

    public List<string> Labels => this.autoLoadLabels;

    public void UpdateLabels(List<string> newLabels)
    {
        this.autoLoadLabels.Clear();

        int count = newLabels.Count;

        for (int i = 0; i < count; i++)
        {
            this.autoLoadLabels.Add(newLabels[i]);
        }
    }

    public void UpdateLabels(HashSet<string> newLabels)
    {
        this.autoLoadLabels.Clear();
        
        newLabels.ExceptWith(this.ignoreLabels);  

        foreach (string label in newLabels)
        {
            this.autoLoadLabels.Add(label);
        }
    }
}
