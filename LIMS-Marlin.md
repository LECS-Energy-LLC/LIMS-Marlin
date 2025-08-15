# LIMS-Marlin

## Backround

LIMS is a software appliance and software toolset for monitoring additive and subtractive manufacturing processes.  A new initiative will add a LIMS-AI appliance that provides real-time anomaly detection AI model training and execution at the edge.

## Problem

One class of machines that use LIMS for monitoring are 3D printers.  It is believed that process problems can likely be detected by using anomaly detection on live printer telemetry.  A pilot is going to be done on Prusa FDM printers.  The printers use the Marlin firmware, and LIMS already supports reading Marlin data.  Marlin, however, only provides access to sensors the printer has, and it's believed that additional sensors will greatly improve the ability to diagnose process problems.

## Proposed Solution

To address these issues, LECS Energy is proposing a hardware appliance called LIMS-Marlin.  It will run a stripped-down version of the existing LIMS software for reading Marlin data and it will add new sensors and software to integrate that telemetry into the LIMS software stack.

The hardware will run on an off-the-shelf Raspberry Pi 4 running Debian lite 64-bit.  This will provide networking, and USB Host for connecting to the printer to ingest the existing Marlin data stream.  A custom "shield" will also be created to add the additional sensors needed.  LECS Energy will create the software to read those sensors and integrate it with the existing system.

This harware will be in an enclosure mechanically affixed to the printer.

### Custom Raspberry Pi shield

The custom shield will cover the existing Raspbberry Pi PC area not including the USB and Ethernet connectors, so will be approximately 55mmx65mm.  It will use only the existing Raspberry Pi 40-pin header.  It will have the following on-board sensors:

- ambient temperature and humidity
- air quality (VoC)
- 3-axis accelerometer
- an ADC for 0-5V sensor input (external current transducer)

It will also have connectors for

- 2-pole phono jack for the CT (connected to the ADC)
- 2-pin power (5V, 150mA source) to drive a 30mm brushless fan (to circulate air for the VoC and temp sensors)

Ability to switch the fan power from a GPIO would be nice, but is not a hard requirement

### Sensors/ICs for integration

The following sensors have been selected for inclusion:

- ENS160 Air Quality
- AHT2x or BME280 Temp/Humidity
- MPU6050 3-axis accelerometer
- ADS1115 16-bit ADC


### Hardware selection

Due to low-volume, it may be less expensive and faster to use off-the-shelf already-integrated sensor modules that get soldered to the custom shield instead of using individual components.  Below is a list of candicates to investigate:


- Combined ENS160/AHT2x sensor: https://amzn.to/433swGF
- MPU6050 module: https://amzn.to/3YziMm9
- ADS1115 module: https://amzn.to/4iQ60Xm


This Waveshare board has many of the sensors (except the ADC) and is provided as a reference/isea of the concept: https://amzn.to/4m1aYnj
## Pilot Project

The initial pilot project is 10 printers.  The facility/customer will provide:

- 5V USB power for the Raspberry Pi
- RJ45 network cable to a switch on the same network as the LIMS and LIMS-AI boxes


## Installing

- Flash an SD card with Raspberry Pi OS Lite 64-bit
- log in
```
$ sudo chmod 666 /sys/class/leds/ACT/brightness
$ sudo raspi-config
```
Enable I2C