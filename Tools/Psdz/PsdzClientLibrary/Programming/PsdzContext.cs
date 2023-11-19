﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BMW.Rheingold.CoreFramework.Contracts.Vehicle;
using BMW.Rheingold.Programming.API;
using BMW.Rheingold.Programming.Common;
using BMW.Rheingold.Psdz;
using BMW.Rheingold.Psdz.Model;
using BMW.Rheingold.Psdz.Model.Ecu;
using BMW.Rheingold.Psdz.Model.Svb;
using BMW.Rheingold.Psdz.Model.Swt;
using BMW.Rheingold.Psdz.Model.Tal;
using BMW.Rheingold.Psdz.Model.Tal.TalFilter;
using BmwFileReader;
using PsdzClient.Core;
using PsdzClient.Utility;
using PsdzClientLibrary.Core;

namespace PsdzClient.Programming
{
	public class PsdzContext : IPsdzContext, IDisposable
	{
        private const string IdrBackupFileName = "_IDR_Files.backup";

        public enum BackupTalResult
        {
            Success,
            Failed,
            Error,
            Undefined,
            SuccessEmpty
        }
		
        public PsdzContext(string istaFolder)
        {
            this.IstaFolder = istaFolder;
			this.ExecutionOrderTop = new Dictionary<string, IList<string>>();
			this.ExecutionOrderBottom = new Dictionary<string, IList<string>>();
		}

		public bool IsEmptyBackupTal
		{
			get
			{
				return !this.IsValidBackupTal || !this.IndividualDataBackupTal.TalLines.Any<IPsdzTalLine>();
			}
		}

		public bool IsEmptyPrognosisTal
		{
			get
			{
				return !this.IsValidRestorePrognosisTal || !this.IndividualDataRestorePrognosisTal.TalLines.Any<IPsdzTalLine>();
			}
		}

		public bool IsEmptyRestoreTal
		{
			get
			{
				return !this.IsValidRestoreTal || !this.IndividualDataRestoreTal.TalLines.Any<IPsdzTalLine>();
			}
		}

		public bool IsEmptyTal
		{
			get
			{
				return !this.IsValidTal || !this.Tal.TalLines.Any<IPsdzTalLine>();
			}
		}

        public BackupTalResult CheckBackupTal()
        {
            if (!IsValidBackupTal)
            {
                return BackupTalResult.Error;
            }
            if (IsEmptyBackupTal)
            {
                return BackupTalResult.Success;
            }
            return BackupTalResult.Undefined;
        }

        public string IstufeCurrent { get; private set; }

		public string IstufeLast { get; private set; }

		public string IstufeShipment { get; private set; }

		public bool IsValidBackupTal
		{
			get
			{
				return this.IndividualDataBackupTal != null;
			}
		}

		public bool IsValidEcuListActual
		{
			get
			{
				return this.EcuListActual != null && this.EcuListActual.Any<IPsdzEcuIdentifier>();
			}
		}

		public bool IsValidFaActual
		{
			get
			{
				return this.FaActual != null && this.FaActual.IsValid;
			}
		}

		public bool IsValidFaTarget
		{
			get
			{
				return this.FaTarget != null && this.FaTarget.IsValid;
			}
		}

		public bool IsValidRestorePrognosisTal
		{
			get
			{
				return this.IndividualDataRestorePrognosisTal != null;
			}
		}

		public bool IsValidRestoreTal
		{
			get
			{
				return this.IndividualDataRestoreTal != null;
			}
		}

		public bool IsValidSollverbauung
		{
			get
			{
				return this.Sollverbauung != null;
			}
		}

		public bool IsValidSvtActual
		{
			get
			{
				return this.SvtActual != null && this.SvtActual.IsValid;
			}
		}

		public bool IsValidTal
		{
			get
			{
				return this.Tal != null;
			}
		}

		public string LatestPossibleIstufeTarget
		{
			get
			{
				IEnumerable<IPsdzIstufe> enumerable = this.possibleIstufenTarget;
				if (enumerable != null && enumerable.Any<IPsdzIstufe>())
				{
					return enumerable.Last<IPsdzIstufe>().Value;
				}
				return null;
			}
		}

		public string ProjectName { get; set; }

		public IPsdzSwtAction SwtAction { get; set; }

		public string VehicleInfo { get; set; }

		public string PathToBackupData { get; set; }

        public bool PsdZBackUpModeSet { get; set; }

		public IPsdzTalFilter TalFilterForIndividualDataTal { get; private set; }

        public IPsdzConnection Connection { get; set; }

        public DetectVehicle DetectVehicle { get; set; }

        public Vehicle VecInfo { get; set; }

        public IEnumerable<IPsdzEcuIdentifier> EcuListActual { get; set; }

        public IDictionary<string, IList<string>> ExecutionOrderBottom { get; private set; }

        public IDictionary<string, IList<string>> ExecutionOrderTop { get; private set; }

        public IPsdzFa FaActual { get; private set; }

        public IPsdzFa FaTarget { get; private set; }

        public IPsdzTal IndividualDataBackupTal { get; set; }

        public IPsdzTal IndividualDataRestorePrognosisTal { get; set; }

        public IPsdzTal IndividualDataRestoreTal { get; set; }

        public IPsdzSollverbauung Sollverbauung { get; private set; }

        public IPsdzSvt SvtActual { get; private set; }

        public IPsdzTal Tal { get; set; }

        public IPsdzTalFilter TalFilter { get; private set; }

        public IPsdzTalFilter TalFilterForECUWithIDRClassicState { get; private set; }

        public IPsdzTal TalForECUWithIDRClassicState { get; set; }

        public IEnumerable<IPsdzTargetSelector> TargetSelectors { get; set; }

        public string IstaFolder { get; private set; }

        public BaseEcuCharacteristics EcuCharacteristics { get; private set; }

        public string GetBaseVariant(int diagnosticAddress)
		{
			if (this.SvtActual.Ecus.Any((IPsdzEcu ecu) => diagnosticAddress == ecu.PrimaryKey.DiagAddrAsInt))
			{
				return this.SvtActual.Ecus.Single((IPsdzEcu ecu) => diagnosticAddress == ecu.PrimaryKey.DiagAddrAsInt).BaseVariant;
			}
			return string.Empty;
		}

        public ICombinedEcuHousingEntry GetEcuHousingEntry(int diagnosticAddress)
        {
            if (EcuCharacteristics == null || EcuCharacteristics.combinedEcuHousingTable == null)
            {
                return null;
            }

            foreach (ICombinedEcuHousingEntry combinedEcuHousingEntry in EcuCharacteristics.combinedEcuHousingTable)
            {
                int[] requiredEcuAddresses = combinedEcuHousingEntry.RequiredEcuAddresses;
                if (requiredEcuAddresses != null)
                {
                    foreach (int ecuAddress in requiredEcuAddresses)
                    {
                        if (ecuAddress == diagnosticAddress)
                        {
                            return combinedEcuHousingEntry;
                        }
                    }
                }
            }

            return null;
        }

        public IEcuLogisticsEntry GetEcuLogisticsEntry(int diagnosticAddress)
        {
            if (EcuCharacteristics == null)
            {
                return null;
            }

            foreach (IEcuLogisticsEntry ecuLogisticsEntry in EcuCharacteristics.ecuTable)
            {
                if (diagnosticAddress == ecuLogisticsEntry.DiagAddress)
                {
                    return ecuLogisticsEntry;
                }
            }

            return null;
        }

        public bool SetPathToBackupData(string vin17)
		{
			this.hasVinBackupDataFolder = false;
			string pathString = Path.Combine(IstaFolder, "Temp");
			if (string.IsNullOrEmpty(pathString))
			{
				this.PathToBackupData = null;
				return false;
			}
			if (string.IsNullOrEmpty(vin17))
			{
				this.PathToBackupData = Path.GetFullPath(pathString);
			}
            else
			{
				this.hasVinBackupDataFolder = true;
                this.PathToBackupData = Path.GetFullPath(Path.Combine(pathString, vin17));
            }

            if (!string.IsNullOrEmpty(PathToBackupData) && !Directory.Exists(this.PathToBackupData))
			{
				Directory.CreateDirectory(this.PathToBackupData);
			}

            return true;
        }

        public void CleanupBackupData()
        {
            if (!string.IsNullOrEmpty(PathToBackupData) && this.hasVinBackupDataFolder &&
                Directory.Exists(PathToBackupData) && !Directory.EnumerateFileSystemEntries(PathToBackupData).Any<string>())
            {
                Directory.Delete(PathToBackupData);
            }

            if (!HasBackupDataDir())
            {
                this.hasVinBackupDataFolder = false;
            }
        }

        public bool HasBackupDataDir()
        {
            if (!string.IsNullOrEmpty(PathToBackupData) && this.hasVinBackupDataFolder && Directory.Exists(PathToBackupData))
            {
                return true;
            }

            return false;
        }

        public bool HasBackupData()
        {
            if (!string.IsNullOrEmpty(PathToBackupData) && this.hasVinBackupDataFolder &&
                    Directory.Exists(PathToBackupData) && Directory.EnumerateFileSystemEntries(PathToBackupData).Any<string>())
            {
                return true;
            }

            return false;
        }

        public bool RemoveBackupData()
        {
            if (!string.IsNullOrEmpty(PathToBackupData) && this.hasVinBackupDataFolder)
            {
                try
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(PathToBackupData);
                    if (directoryInfo.Exists)
                    {
                        foreach (FileInfo file in directoryInfo.GetFiles())
                        {
                            file.Delete();
                        }

                        foreach (DirectoryInfo dir in directoryInfo.GetDirectories())
                        {
                            dir.Delete();
                        }
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }

        public bool SaveIDRFilesToPuk()
        {
            try
            {
                if (!HasBackupData())
                {
                    return false;
                }

                string backupPath = PathToBackupData;
                string backupFile = backupPath.TrimEnd('\\') + IdrBackupFileName;
                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }

                System.IO.Compression.ZipFile.CreateFromDirectory(backupPath, backupFile);

                return true;
            }
            catch (Exception)
            {
                // ignored
            }

            return false;
        }

        public bool GetIDRFilesFromPuk()
        {
            try
            {
                if (!HasIDRFilesInPuk())
                {
                    return false;
                }

                string backupPath = PathToBackupData;
                string backupFile = backupPath.TrimEnd('\\') + IdrBackupFileName;
                if (!File.Exists(backupFile))
                {
                    return false;
                }

                System.IO.Compression.ZipFile.ExtractToDirectory(backupFile, backupPath);

                return true;
            }
            catch (Exception)
            {
                // ignored
            }

            return false;
        }

        public bool HasIDRFilesInPuk()
        {
            try
            {
                if (!HasBackupDataDir())
                {
                    return false;
                }

                string backupPath = PathToBackupData;
                string backupFile = backupPath.TrimEnd('\\') + IdrBackupFileName;
                if (!File.Exists(backupFile))
                {
                    return false;
                }

                return true;
            }
            catch (Exception)
            {
                // ignored
            }

            return false;
        }

        public bool DeleteIDRFilesFromPuk()
        {
            try
            {
                if (!HasBackupDataDir())
                {
                    return false;
                }

                string backupPath = PathToBackupData;
                string backupFile = backupPath.TrimEnd('\\') + IdrBackupFileName;
                if (File.Exists(backupFile))
                {
                    File.Delete(backupFile);
                }

                return true;
            }
            catch (Exception)
            {
                // ignored
            }

            return false;
        }

        public void SetFaActual(IPsdzFa fa)
		{
			this.FaActual = fa;
            if (VecInfo != null)
            {
                VecInfo.FA = ProgrammingUtils.BuildVehicleFa(fa, DetectVehicle.BrName);
            }
		}

		public void SetFaTarget(IPsdzFa fa)
		{
			this.FaTarget = fa;
            if (VecInfo != null)
            {
                VecInfo.TargetFA = ProgrammingUtils.BuildVehicleFa(fa, DetectVehicle.BrName);
            }
		}

		public void SetIstufen(IPsdzIstufenTriple istufenTriple)
		{
			if (istufenTriple != null)
			{
				this.IstufeShipment = istufenTriple.Shipment;
				this.IstufeLast = istufenTriple.Last;
				this.IstufeCurrent = istufenTriple.Current;
				return;
			}
			this.IstufeShipment = null;
			this.IstufeLast = null;
			this.IstufeCurrent = null;
		}

        public void SetPossibleIstufenTarget(IEnumerable<IPsdzIstufe> possibleIstufenTarget)
		{
			this.possibleIstufenTarget = possibleIstufenTarget;
		}

        public void SetSollverbauung(IPsdzSollverbauung sollverbauung)
		{
			this.Sollverbauung = sollverbauung;
		}

        public void SetSvtActual(IPsdzSvt svt)
		{
			this.SvtActual = svt;
		}

        public void SetTalFilter(IPsdzTalFilter talFilter)
		{
			this.TalFilter = talFilter;
		}

        public void SetTalFilterForECUWithIDRClassicState(IPsdzTalFilter talFilter)
		{
			this.TalFilterForECUWithIDRClassicState = talFilter;
		}

        public void SetTalFilterForIndividualDataTal(IPsdzTalFilter talFilterForIndividualDataTal)
		{
			this.TalFilterForIndividualDataTal = talFilterForIndividualDataTal;
		}

        public bool UpdateVehicle(ProgrammingService programmingService)
        {
            EcuCharacteristics = null;
            if (VecInfo == null)
            {
                return false;
            }

            ProgrammingObjectBuilder programmingObjectBuilder = programmingService?.ProgrammingInfos?.ProgrammingObjectBuilder;
            if (programmingObjectBuilder == null)
            {
                return false;
            }

            VecInfo.VehicleIdentLevel = IdentificationLevel.VINVehicleReadout;
            VecInfo.ILevelWerk = !string.IsNullOrEmpty(IstufeShipment) ? IstufeShipment : DetectVehicle.ILevelShip;
            VecInfo.ILevel = !string.IsNullOrEmpty(IstufeCurrent) ? IstufeCurrent: DetectVehicle.ILevelCurrent;
            VecInfo.VIN17 = DetectVehicle.Vin;

            if (DetectVehicle.ConstructDate != null)
            {
                VecInfo.Modelljahr = DetectVehicle.ConstructYear;
                VecInfo.Modellmonat = DetectVehicle.ConstructMonth;
                VecInfo.Modelltag = "01";

                if (string.IsNullOrEmpty(VecInfo.BaustandsJahr) || string.IsNullOrEmpty(VecInfo.BaustandsMonat))
                {
                    VecInfo.BaustandsJahr = DetectVehicle.ConstructDate.Value.ToString("yy", CultureInfo.InvariantCulture);
                    VecInfo.BaustandsMonat = DetectVehicle.ConstructDate.Value.ToString("MM", CultureInfo.InvariantCulture);
                }

                if (!VecInfo.FA.AlreadyDone)
                {
                    if (string.IsNullOrEmpty(VecInfo.FA.C_DATE))
                    {
                        VecInfo.FA.C_DATE = DetectVehicle.ConstructDate.Value.ToString("MMyy", CultureInfo.InvariantCulture);
                    }

                    if (VecInfo.FA.C_DATETIME == null)
                    {
                        VecInfo.FA.C_DATETIME = DetectVehicle.ConstructDate.Value;
                    }
                }
            }

            PsdzDatabase.VinRanges vinRangesByVin = programmingService.PsdzDatabase.GetVinRangesByVin17(VecInfo.VINType, VecInfo.VIN7, VecInfo.IsVehicleWithOnlyVin7());
            if (vinRangesByVin != null)
            {
                VecInfo.VINRangeType = vinRangesByVin.TypeKey;
                if (string.IsNullOrEmpty(VecInfo.Modellmonat) || string.IsNullOrEmpty(VecInfo.Modelljahr))
                {
                    if (!string.IsNullOrEmpty(vinRangesByVin.ProductionYear) && !string.IsNullOrEmpty(vinRangesByVin.ProductionMonth))
                    {
                        VecInfo.Modelljahr = vinRangesByVin.ProductionYear;
                        VecInfo.Modellmonat = vinRangesByVin.ProductionMonth.PadLeft(2, '0');
                        VecInfo.Modelltag = "01";

                        if (VecInfo.Modelljahr.Length == 4)
                        {
                            VecInfo.BaustandsJahr = VecInfo.Modelljahr.Substring(2, 2);
                            VecInfo.BaustandsMonat = VecInfo.Modellmonat;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(VecInfo.Modellmonat) && !string.IsNullOrEmpty(VecInfo.Modelljahr))
            {
                VecInfo.ProductionDate = DateTime.ParseExact(string.Format(CultureInfo.InvariantCulture, "{0}.{1}",
                    VecInfo.Modellmonat, VecInfo.Modelljahr), "MM.yyyy", new CultureInfo("de-DE"));
                VecInfo.ProductionDateSpecified = true;
            }

            VecInfo.Ereihe = DetectVehicle.Series;

            ClientContext clientContext = ClientContext.GetClientContext(VecInfo);
            SetFa(programmingService);
            CharacteristicExpression.EnumBrand brand = CharacteristicExpression.EnumBrand.BMWBMWiMINI;
            if (VecInfo.IsMotorcycle())
            {
                brand = CharacteristicExpression.EnumBrand.BMWMotorrad;
            }

            if (clientContext != null)
            {
                clientContext.SelectedBrand = brand;
            }

            for (int i = 0; i < 2; i++)
            {
                ObservableCollection<ECU> EcuList = new ObservableCollection<ECU>();
                foreach (PsdzDatabase.EcuInfo ecuInfo in DetectVehicle.EcuListPsdz)
                {
                    IEcuObj ecuObj = programmingObjectBuilder.Build(ecuInfo.PsdzEcu);
                    ECU ecu = programmingObjectBuilder.Build(ecuObj);
                    if (ecu != null)
                    {
                        if (string.IsNullOrEmpty(ecu.ECU_NAME))
                        {
                            ecu.ECU_NAME = ecuInfo.Name;
                        }

                        if (string.IsNullOrEmpty(ecu.ECU_SGBD))
                        {
                            ecu.ECU_SGBD = ecuInfo.Sgbd;
                        }

                        if (string.IsNullOrEmpty(ecu.ECU_GRUPPE))
                        {
                            ecu.ECU_GRUPPE = ecuInfo.Grp;
                        }
                        EcuList.Add(ecu);
                    }
                }

                VecInfo.ECU = EcuList;
            }

            IDiagnosticsBusinessData service = ServiceLocator.Current.GetService<IDiagnosticsBusinessData>();
            List<PsdzDatabase.Characteristics> characteristicsList = programmingService.PsdzDatabase.GetVehicleCharacteristics(VecInfo);
            if (characteristicsList == null)
            {
                return false;
            }

            if (!UpdateAllVehicleCharacteristics(characteristicsList, programmingService.PsdzDatabase, VecInfo))
            {
                return false;
            }

            UpdateSALocalizedItems(programmingService, clientContext);

            VecInfo.FA.AlreadyDone = true;
            if (VecInfo.ECU != null && VecInfo.ECU.Count > 1)
            {
                VecInfo.VehicleIdentAlreadyDone = true;
            }
            else
            {
                CalculateECUConfiguration();
            }

            VecInfo.BatteryType = PsdzDatabase.ResolveBatteryType(VecInfo);
            VecInfo.WithLfpBattery = VecInfo.BatteryType == PsdzDatabase.BatteryEnum.LFP;
            VecInfo.MainSeriesSgbd = DetectVehicle.GroupSgbd;

            // DetectVehicle.SgbdAdd ist calculated by GetMainSeriesSgbdAdditional anyway
            VecInfo.MainSeriesSgbdAdditional = service.GetMainSeriesSgbdAdditional(VecInfo);

            PerformVecInfoAssignments();

            EcuCharacteristics = VehicleLogistics.GetCharacteristics(VecInfo);
            return true;
        }

        public static bool AssignVehicleCharacteristics(List<PsdzDatabase.Characteristics> characteristics, Vehicle vehicle)
        {
            if (vehicle == null)
            {
                return false; 
            }

            VehicleCharacteristicIdent vehicleCharacteristicIdent = new VehicleCharacteristicIdent();
            foreach (PsdzDatabase.Characteristics characteristic in characteristics)
            {
                if (string.IsNullOrEmpty(vehicle.VerkaufsBezeichnung) || !(characteristic.RootNodeClass == "40143490"))
                {
                    if (!vehicleCharacteristicIdent.AssignVehicleCharacteristic(characteristic.RootNodeClass, vehicle, characteristic))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public static bool UpdateAlpinaCharacteristics(PsdzDatabase database, Vehicle vehicle)
        {
            List<PsdzDatabase.Characteristics> list = new List<PsdzDatabase.Characteristics>();
            database.GetAlpinaCharacteristics(vehicle, list);
            if (list.Any())
            {
                return AssignVehicleCharacteristics(list, vehicle);
            }

            return true;
        }

        public static bool UpdateAllVehicleCharacteristics(List<PsdzDatabase.Characteristics> characteristics, PsdzDatabase database, Vehicle vehicle)
        {
            if (database == null || vehicle == null)
            {
                return false;
            }

            if (!AssignVehicleCharacteristics(characteristics, vehicle))
            {
                return false;
            }

            IDiagnosticsBusinessData service = ServiceLocator.Current.GetService<IDiagnosticsBusinessData>();
            string typsnr = !string.IsNullOrEmpty(vehicle.Typ) ? vehicle.Typ : vehicle.VINType;
            service.SpecialTreatmentBasedOnEreihe(typsnr, vehicle);

            if (!UpdateAlpinaCharacteristics(database, vehicle))
            {
                return false;
            }

            return true;
        }

        // ToDo: Check on update
        private void PerformVecInfoAssignments()
        {
            try
            {
                if (VecInfo == null)
                {
                    return;
                }
                if (VecInfo.ECU != null && VecInfo.ECU.Count > 0)
                {   // [UH] ECU check added
                    GearboxUtility.PerformGearboxAssignments(VecInfo);
                }
                if (VecInfo.BNType == BNType.UNKNOWN && !string.IsNullOrEmpty(VecInfo.Ereihe))
                {
                    IDiagnosticsBusinessData service = ServiceLocator.Current.GetService<IDiagnosticsBusinessData>();
                    VecInfo.BNType = service.GetBNType(VecInfo);
                }
                if (VecInfo.BNMixed == BNMixed.UNKNOWN)
                {
                    VecInfo.BNMixed = VehicleLogistics.getBNMixed(VecInfo.Ereihe, VecInfo.FA);
                }
                if (string.IsNullOrEmpty(VecInfo.Prodart))
                {
                    if (!VecInfo.IsMotorcycle())
                    {
                        VecInfo.Prodart = "P";
                    }
                    else
                    {
                        VecInfo.Prodart = "M";
                    }
                }
                if ((string.IsNullOrEmpty(VecInfo.Lenkung) || VecInfo.Lenkung == "UNBEK" || VecInfo.Lenkung.Trim() == string.Empty) && (!string.IsNullOrEmpty(VecInfo.VINType) & (VecInfo.VINType.Length == 4)))
                {
                    switch (VecInfo.VINType[3])
                    {
                        default:
                            VecInfo.Lenkung = "LL";
                            break;
                        case '1':
                        case '3':
                        case '5':
                        case 'C':
                            VecInfo.Lenkung = "LL";
                            break;
                        case '2':
                        case '6':
                            VecInfo.Lenkung = "RL";
                            break;
                    }
                }
                if (string.IsNullOrWhiteSpace(VecInfo.BaseVersion) && (!string.IsNullOrEmpty(VecInfo.VINType) & (VecInfo.VINType.Length == 4)))
                {
                    switch (VecInfo.VINType[3])
                    {
                        case '3':
                        case 'C':
                            VecInfo.BaseVersion = "US";
                            break;
                        case '1':
                        case '5':
                            VecInfo.BaseVersion = "ECE";
                            break;
                    }
                }
                if (string.IsNullOrEmpty(VecInfo.Land) || VecInfo.Land == "UNBEK")
                {
                    if (!string.IsNullOrEmpty(VecInfo.VINType) & (VecInfo.VINType.Length == 4))
                    {
                        switch (VecInfo.VINType[3])
                        {
                            default:
                                VecInfo.Land = "EUR";
                                break;
                            case '1':
                            case '2':
                                VecInfo.Land = "EUR";
                                break;
                            case '3':
                            case '4':
                            case 'C':
                                VecInfo.Land = "USA";
                                break;
                        }
                    }
                    if (VecInfo.hasSA("807") && VecInfo.Prodart == "P")
                    {
                        VecInfo.Land = "JP";
                    }
                    if (VecInfo.hasSA("8AA") && VecInfo.Prodart == "P")
                    {
                        VecInfo.Land = "CHN";
                    }
                }
                if (string.IsNullOrEmpty(VecInfo.Modelljahr) && !string.IsNullOrEmpty(VecInfo.ILevelWerk))
                {
                    try
                    {
                        if (Regex.IsMatch(VecInfo.ILevelWerk, "^\\w{4}[_\\-]\\d{2}[_\\-]\\d{2}[_\\-]\\d{3}$"))
                        {
                            VecInfo.BaustandsJahr = VecInfo.ILevelWerk.Substring(5, 2);
                            VecInfo.BaustandsMonat = VecInfo.ILevelWerk.Substring(8, 2);
                            int num2 = Convert.ToInt32(VecInfo.ILevelWerk.Substring(5, 2), CultureInfo.InvariantCulture);
                            VecInfo.Modelljahr = ((num2 <= 50) ? (num2 + 2000) : (num2 + 1900)).ToString(CultureInfo.InvariantCulture);
                            VecInfo.Modellmonat = VecInfo.ILevelWerk.Substring(8, 2);
                            VecInfo.Modelltag = "01";
                            Log.Info("Missing construction date (year: {0}, month: {1}) retrieved from iLevel plant ('{2}')", VecInfo.Modelljahr, VecInfo.Modellmonat, VecInfo.ILevelWerk);
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.WarningException("VehicleIdent.finalizeFASTAHeader()", exception);
                    }
                }
                if (string.IsNullOrEmpty(VecInfo.MainSeriesSgbd))
                {   // [UH] simplified
                    VecInfo.MainSeriesSgbd = VehicleLogistics.getBrSgbd(VecInfo);
                }
                if (string.IsNullOrEmpty(VecInfo.Abgas))
                {
                    VecInfo.Abgas = "KAT";
                }
                if (!string.IsNullOrEmpty(VecInfo.Motor) && !(VecInfo.Motor == "UNBEK"))
                {
                    return;
                }
                ECU eCUbyECU_GRUPPE = VecInfo.getECUbyECU_GRUPPE("D_MOTOR");
                if (eCUbyECU_GRUPPE == null)
                {
                    eCUbyECU_GRUPPE = VecInfo.getECUbyECU_GRUPPE("G_MOTOR");
                }
                if (eCUbyECU_GRUPPE != null && !string.IsNullOrEmpty(eCUbyECU_GRUPPE.VARIANTE))
                {
                    Match match = Regex.Match(eCUbyECU_GRUPPE.VARIANTE, "[SNM]\\d\\d");
                    if (match.Success)
                    {
                        VecInfo.Motor = match.Value;
                    }
                }
            }
            catch (Exception exception2)
            {
                Log.WarningException("VehicleIdent.finalizeFASTAHeader()", exception2);
            }
        }

        private void CalculateECUConfiguration()
        {
            if (VecInfo.BNType != BNType.BN2020_MOTORBIKE && VecInfo.BNType != BNType.BNK01X_MOTORBIKE && VecInfo.BNType != BNType.BN2000_MOTORBIKE)
            {
                if (VecInfo.BNType == BNType.IBUS)
                {
                    VehicleLogistics.CalculateECUConfiguration(VecInfo, null);
                    if (VecInfo.ECU != null && VecInfo.ECU.Count > 1)
                    {
                        VecInfo.VehicleIdentAlreadyDone = true;
                    }
                }
                return;
            }

            VehicleLogistics.CalculateECUConfiguration(VecInfo, null);
            if (VecInfo.ECU != null && VecInfo.ECU.Count > 1)
            {
                VecInfo.VehicleIdentAlreadyDone = true;
            }
        }

        public List<PsdzDatabase.EcuInfo> GetEcuList(bool individualOnly = false)
        {
            List<PsdzDatabase.EcuInfo> ecuList = new List<PsdzDatabase.EcuInfo>();
            try
            {
                foreach (PsdzDatabase.EcuInfo ecuInfo in DetectVehicle.EcuListPsdz)
                {
                    if (individualOnly)
                    {
                        if (VecInfo.IsMotorcycle() || ecuInfo.HasIndividualData)
                        {
                            ecuList.Add(ecuInfo);
                        }
                    }
                    else
                    {
                        ecuList.Add(ecuInfo);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return ecuList;
        }

        public bool SetFa(ProgrammingService programmingService)
        {
            try
            {
                if (VecInfo.FA.AlreadyDone)
                {
                    return true;
                }

                if (string.IsNullOrEmpty(VecInfo.FA.LACK))
                {
                    VecInfo.FA.LACK = DetectVehicle.Paint;
                }

                if (string.IsNullOrEmpty(VecInfo.FA.POLSTER))
                {
                    VecInfo.FA.POLSTER = DetectVehicle.Upholstery;
                }

                if (string.IsNullOrEmpty(VecInfo.FA.STANDARD_FA))
                {
                    VecInfo.FA.STANDARD_FA = DetectVehicle.StandardFa;
                }

                if (string.IsNullOrEmpty(VecInfo.FA.TYPE))
                {
                    VecInfo.FA.TYPE = DetectVehicle.TypeKey;
                }

                if (VecInfo.FA.SA.Count == 0 && VecInfo.FA.HO_WORT.Count == 0 &&
                    VecInfo.FA.E_WORT.Count == 0 && VecInfo.FA.ZUSBAU_WORT.Count == 0)
                {
                    foreach (string salapa in DetectVehicle.Salapa)
                    {
                        VecInfo.FA.SA.AddIfNotContains(salapa);
                    }

                    foreach (string hoWord in DetectVehicle.HoWords)
                    {
                        VecInfo.FA.HO_WORT.AddIfNotContains(hoWord);
                    }

                    foreach (string eWord in DetectVehicle.EWords)
                    {
                        VecInfo.FA.E_WORT.AddIfNotContains(eWord);
                    }

                    foreach (string zbWord in DetectVehicle.ZbWords)
                    {
                        VecInfo.FA.ZUSBAU_WORT.AddIfNotContains(zbWord);
                    }

                    OverrideVehicleCharacteristics(programmingService);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public bool UpdateSALocalizedItems(ProgrammingService programmingService, ClientContext clientContext)
        {
            try
            {
                if (clientContext == null)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(VecInfo.Prodart) || VecInfo.BrandName == null)
                {
                    return false;
                }

                string language = clientContext.Language;
                string prodArt = PsdzDatabase.GetProdArt(VecInfo);

                FillSaLocalizedItems(programmingService, language, DetectVehicle.Salapa, prodArt);
                FillSaLocalizedItems(programmingService, language, DetectVehicle.HoWords, prodArt);
                FillSaLocalizedItems(programmingService, language, DetectVehicle.EWords, prodArt);
                FillSaLocalizedItems(programmingService, language, DetectVehicle.ZbWords, prodArt);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public void FillSaLocalizedItems(ProgrammingService programmingService, string language, List<string> source, string prodArt)
        {
            foreach (string text in source)
            {
                string key = FormatConverter.FillWithZeros(text, 4);
                if (VecInfo.FA.SaLocalizedItems.FirstOrDefault(x => x.Id == key) == null)
                {
                    PsdzDatabase.SaLaPa saLaPa = programmingService.PsdzDatabase.GetSaLaPaByProductTypeAndSalesKey(prodArt, key);
                    if (saLaPa != null)
                    {
                        VecInfo.FA.SaLocalizedItems.Add(new LocalizedSAItem(key, saLaPa.EcuTranslation.GetTitle(language)));
                    }
                }
            }
        }

        public bool OverrideVehicleCharacteristics(ProgrammingService programmingService)
        {
            try
            {
                if (VecInfo == null)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(VecInfo.VINRangeType))
                {
                    List<Tuple<string, string>> transmissionSaByTypeKey = programmingService.PsdzDatabase.GetTransmissionSaByTypeKey(VecInfo.VINRangeType);
                    if (transmissionSaByTypeKey == null)
                    {
                        return false;
                    }

                    if (!transmissionSaByTypeKey.Any(sa => VecInfo.hasSA(sa.Item1)))
                    {
                        List<Tuple<string, string>> list = transmissionSaByTypeKey.Where(sa => sa.Item2 == "T").ToList();
                        if (list.Count == 1)
                        {
                            string text = list.First().Item1;
                            if (string.IsNullOrEmpty(text))
                            {
                                VecInfo.FA.SA.Add(text);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }


        public string GetLocalizedSaString()
        {
            StringBuilder sb = new StringBuilder();
            if (VecInfo.FA != null && VecInfo.FA.SaLocalizedItems.Count > 0)
            {
                foreach (LocalizedSAItem saLocalized in VecInfo.FA.SaLocalizedItems)
                {
                    if (saLocalized != null)
                    {
                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "SA={0}, Title='{1}'", saLocalized.Id, saLocalized.Title));
                    }
                }
            }

            return sb.ToString();
        }

        private bool _disposed;
		private bool hasVinBackupDataFolder;

		private IEnumerable<IPsdzIstufe> possibleIstufenTarget;

        public void Dispose()
        {
            Dispose(true);
            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SupressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!_disposed)
            {
                if (DetectVehicle != null)
                {
                    DetectVehicle.Dispose();
                    DetectVehicle = null;
                }

                VecInfo = null;

				// If disposing equals true, dispose all managed
				// and unmanaged resources.
				if (disposing)
                {
                }

                // Note disposing has been done.
                _disposed = true;
            }
        }

	}
}
