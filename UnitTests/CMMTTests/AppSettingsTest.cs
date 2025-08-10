using CMMT.DataStructures;
using CMMT.Models;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.Reflection;

namespace CMMTTests
{
    [TestFixture]
    public class AppSettingsTests
    {
        [Test]
        public void TestGetInstance()
        {
            // Arrange
            var instance1 = AppSettingsModel.GetInstance;
            var instance2 = AppSettingsModel.GetInstance;
            // Act
            bool areSameInstance = ReferenceEquals(instance1, instance2);
            // Assert
            Assert.That(areSameInstance, Is.True, "GetInstance should return the same instance every time.");
            ClassicAssert.IsTrue(areSameInstance, "GetInstance should return the same instance every time.");
        }

        /// <summary>
        /// 
        /// </summary>

        [Test]
        public void TestLoadAppSettings()
        {
            // Arrange
            var appSettingsModel = AppSettingsModel.GetInstance;
            // Act
            appSettingsModel.LoadAppSettings();
            // Assert
            ClassicAssert.IsNotNull(appSettingsModel.Settings, "Settings should not be null after loading.");
            ClassicAssert.IsNotNull(appSettingsModel.Settings.CsvTypes, "Csv types should not be null after loading.");
        }

        [Test]
        public void TestDefaultSettings()
        {
            // Arrange
            var appSettingsModel = AppSettingsModel.GetInstance;
            // Act
            var appSettings = appSettingsModel.GetDefaultAppSettings();
            // Assert
            ClassicAssert.IsNotNull(appSettings, "Default settings should not be null.");
            ClassicAssert.AreEqual(appSettings.MaxParallel, 2);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestCorrectAndSaveAppSettings(bool loadStatus)
        {
            // Arrange
            var appSettingsModel = AppSettingsModel.GetInstance;
            appSettingsModel.LoadAppSettings();
            var invalidSettings = GetInvalidSettings(appSettingsModel.Settings);

            // Act
            appSettingsModel.CorrectAndSaveAppSettings(loadStatus, invalidSettings);

            // Assert
            ClassicAssert.IsNotNull(appSettingsModel.Settings, "App settings should not be null after correction and saving.");
            ClassicAssert.AreEqual(appSettingsModel.Settings.MaxParallel, 2, "MaxParallel should be corrected to 2 if it was invalid.");
        }

        private List<PropertyInfo> GetInvalidSettings(AppSettings appSettings)
        {
            var invalidSettings = new List<PropertyInfo>();
            appSettings.MaxParallel = 4; // Default value for testing

            if (appSettings.MaxParallel > 2)
            {
                invalidSettings.Add(appSettings.GetType().GetProperty(nameof(appSettings.MaxParallel)));
            }
            
            return invalidSettings;
        }
    }
}
