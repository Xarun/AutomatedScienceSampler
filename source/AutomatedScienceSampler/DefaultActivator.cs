﻿using System;
using System.Collections.Generic;
using System.Text;

namespace KerboKatz.ASS
{
  internal class DefaultActivator : IScienceActivator
  {
    internal static DefaultActivator instance;
    private AutomatedScienceSampler _AutomatedScienceSamplerInstance;

    AutomatedScienceSampler IScienceActivator.AutomatedScienceSampler
    {
      get { return _AutomatedScienceSamplerInstance; }
      set { _AutomatedScienceSamplerInstance = value; }
    }

    public bool CanRunExperiment(ModuleScienceExperiment baseExperiment, float currentScienceValue)
    {
      Log(baseExperiment.experimentID, ": CanRunExperiment");
      if (!baseExperiment.experiment.IsAvailableWhile(ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel), FlightGlobals.currentMainBody))//
      {
        Log(baseExperiment.experimentID, ": Experiment isn't available in the current situation: ", ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel), "_", FlightGlobals.currentMainBody + "_", baseExperiment.experiment.situationMask);
        return false;
      }
      if (baseExperiment.Inoperable)
      {
        Log(baseExperiment.experimentID, ": Experiment is inoperable");
        return false;
      }
      if (baseExperiment.Deployed && !baseExperiment.rerunnable)
      {
        Log(baseExperiment.experimentID, ": Experiment is deployed");
        return false;
      }

      if (!baseExperiment.rerunnable && !_AutomatedScienceSamplerInstance.craftSettings.oneTimeOnly)
      {
        Log(baseExperiment.experimentID, ": Runing rerunable experiments is disabled");
        return false;
      }
      if (currentScienceValue < _AutomatedScienceSamplerInstance.craftSettings.threshold)
      {
        Log(baseExperiment.experimentID, ": Science value is less than cutoff threshold: ", currentScienceValue, "<", _AutomatedScienceSamplerInstance.craftSettings.threshold);
        return false;
      }
      if (baseExperiment.GetData().Length > 0)
      {
        Log(baseExperiment.experimentID, ": Experiment already contains results!");
        return false;
      }
      if (!baseExperiment.experiment.IsUnlocked())
      {
        Log(baseExperiment.experimentID, ": Experiment is locked");
        return false;
      }
      return true;
    }

    public void DeployExperiment(ModuleScienceExperiment baseExperiment)
    {
      Log(baseExperiment.experimentID, ": DeployExperiment");
      if (_AutomatedScienceSamplerInstance.craftSettings.hideScienceDialog)
      {
        var stagingSetting = baseExperiment.useStaging;
        baseExperiment.useStaging = true;//work the way around the staging
        baseExperiment.OnActive();//run the experiment without causing the report to show up
        baseExperiment.useStaging = stagingSetting;//set the staging back
      }
      else
      {
        baseExperiment.DeployExperiment();
      }
    }

    public ScienceSubject GetScienceSubject(ModuleScienceExperiment baseExperiment)
    {
      string currentBiome = CurrentBiome(baseExperiment.experiment);
      return ResearchAndDevelopment.GetExperimentSubject(baseExperiment.experiment, ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel), FlightGlobals.currentMainBody, currentBiome, ScienceUtil.GetBiomedisplayName(FlightGlobals.currentMainBody, currentBiome));
    }

    public float GetScienceValue(ModuleScienceExperiment baseExperiment, Dictionary<string, int> shipCotainsExperiments, ScienceSubject currentScienceSubject)
    {
      return Utilities.Science.GetScienceValue(shipCotainsExperiments, baseExperiment.experiment, currentScienceSubject);
    }

    public bool CanReset(ModuleScienceExperiment baseExperiment)
    {
      Log(baseExperiment.experimentID, ": CanReset");
      if (!baseExperiment.Inoperable)
      {
        Log(baseExperiment.experimentID, ": Experiment isn't inoperable");
        return false;
      }
      if (!baseExperiment.Deployed)
      {
        Log(baseExperiment.experimentID, ": Experiment isn't deployed!");
        return false;
      }
      if (baseExperiment.GetScienceCount() > 0)
      {
        Log(baseExperiment.experimentID, ": Experiment has data!");
        return false;
      }

      if (!baseExperiment.resettable)
      {
        Log(baseExperiment.experimentID, ": Experiment isn't resetable");
        return false;
      }
      bool hasScientist = false;
      foreach (var crew in FlightGlobals.ActiveVessel.GetVesselCrew())
      {
        if (crew.trait == "Scientist")
        {
          hasScientist = true;
          break;
        }
      }
      if (!hasScientist)
      {
        Log(baseExperiment.experimentID, ": Vessel has no scientist");
        return false;
      }
      Log(baseExperiment.experimentID, ": Can reset");
      return true;
    }

    public void Reset(ModuleScienceExperiment baseExperiment)
    {
      Log(baseExperiment.experimentID, ": Reseting experiment");
      baseExperiment.ResetExperiment();
    }

    public bool CanTransfer(ModuleScienceExperiment baseExperiment, IScienceDataContainer moduleScienceContainer)
    {
      Log(baseExperiment.experimentID, ": CanTransfer");

      if (moduleScienceContainer == baseExperiment as IScienceDataContainer)
      {//no point in transfering to the same container
        Log(baseExperiment.experimentID, ": Experiment is same as Container ", baseExperiment.GetScienceCount());
        return false;
      }
      if (baseExperiment.GetScienceCount() == 0)
      {
        Log(baseExperiment.experimentID, ": Experiment has no data skiping transfer ", baseExperiment.GetScienceCount());
        return false;
      }
      if (!baseExperiment.IsRerunnable())
      {
        if (!_AutomatedScienceSamplerInstance.craftSettings.transferAllData)
        {
          Log(baseExperiment.experimentID, ": Experiment isn't rerunnable and transferAllData is turned off.");
          return false;
        }
      }
      if (!_AutomatedScienceSamplerInstance.craftSettings.dumpDuplicates)
      {
        foreach (var data in baseExperiment.GetData())
        {
          if (moduleScienceContainer.HasData(data))
          {
            Log(baseExperiment.experimentID, ": Target already has experiment and dumping is disabled.");
            return false;
          }
        }
      }
      Log(baseExperiment.experimentID, ": We can transfer the science!");
      return true;
    }

    public void Transfer(ModuleScienceExperiment baseExperiment, IScienceDataContainer moduleScienceContainer)
    {
      Log(baseExperiment.experimentID, ": transfering");
      moduleScienceContainer.StoreData(baseExperiment, _AutomatedScienceSamplerInstance.craftSettings.dumpDuplicates);
    }

    private string CurrentBiome(ScienceExperiment baseExperiment)
    {
      if (!baseExperiment.BiomeIsRelevantWhile(ScienceUtil.GetExperimentSituation(FlightGlobals.ActiveVessel)))
        return string.Empty;
      var currentVessel = FlightGlobals.ActiveVessel;
      var currentBody = FlightGlobals.currentMainBody;
      if (currentVessel != null && currentBody != null)
      {
        if (currentVessel.isEVA)
        {
          currentVessel = currentVessel.EVALadderVessel;
        }
        if (!string.IsNullOrEmpty(currentVessel.landedAt))
        {
          //big thanks to xEvilReeperx for this one.
          return Vessel.GetLandedAtString(currentVessel.landedAt);
        }
        else
        {
          return ScienceUtil.GetExperimentBiome(currentBody, currentVessel.latitude, currentVessel.longitude);
        }
      }
      else
      {
        Log("currentVessel && currentBody == null");
      }
      return string.Empty;
    }

    public List<Type> GetValidTypes()
    {
      var types = new List<Type>();
      types.Add(typeof(ModuleScienceExperiment));
      return types;
    }
    private void Log(params object[] msg)
    {
      var debugStringBuilder = new StringBuilder();
      foreach (var debugString in msg)
      {
        debugStringBuilder.Append(debugString.ToString());
      }
      _AutomatedScienceSamplerInstance.Log("[Default]", debugStringBuilder);
    }
  }
}