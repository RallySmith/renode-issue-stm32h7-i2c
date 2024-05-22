// Simple model to provide AsahaKASEI AK4493S basic I2C support
//
// uncomment the following line to get warnings about
// accessing unhandled registers; it's disabled by default
// as it generates a lot of noise in the log and might
// hide some important messages
//
//#define WARN_UNHANDLED_REGISTERS

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;

// Model based on datasheet AK4493S 2022/03 211100143-E-02
//
// I2C mode
//
// Write        {START} [CHIPADDR:0] {ACK} [SUBADDR] {ACK} [DATA0] {ACK} ... [DATAn] {ACK} {STOP}
//
// Read (setup) {START} [CHIPADDR:0] {ACK} [SUBADDR] {ACK} {STOP}
// Read (data)  {START} [CHIPADDR:1] {ACK} [DATA0] {ACK} ... [DATAn] {NAK} {STOP}

namespace Antmicro.Renode.Peripherals.I2C
{
    public class AK4493S : II2CPeripheral, IGPIOReceiver
    {
        public AK4493S()
        {
            registers = CreateRegisters();
            Reset();
        }

        public void Reset()
        {
            lastRegister = 0;
            registers.Reset();
        }

        public void Write(byte[] data)
        {
            if(data.Length == 0)
            {
                this.Log(LogLevel.Warning, "Unexpected write with no data");
                return;
            }

            this.Log(LogLevel.Noisy, "Write {0}", data.Select (x=>"0x"+x.ToString("X2")).Aggregate((x,y)=>x+" "+y));

            lastRegister = (Registers)(data[0] & 0x1F);
            this.Log(LogLevel.Noisy, "Setting register to 0x{0:X}", lastRegister);

            if(0 != (data.Length - 1))
            {
                for (var i = 1; (i < data.Length); i++)
                {
                    this.Log(LogLevel.Debug, "Write register 0x{0:X} value 0x{1:X}", lastRegister, data[i]);
                    registers.Write((long)lastRegister, data[i]);
		    AdvanceRegister();
                }
            }
        }

        public byte[] Read(int count)
        {
            this.Log(LogLevel.Noisy, "Reading {0} bytes from register 0x{1:X}", count, lastRegister);

            var result = new List<byte>();
            for (var i = 0; (i < count); i++)
            {
                result.Add(ReadCurrentRegister());
		AdvanceRegister();
            }

            this.Log(LogLevel.Noisy, "Read result: {0}", Misc.PrettyPrintCollectionHex(result));
            return result.ToArray();
        }

        private void AdvanceRegister()
        {
            lastRegister = (Registers)((int)lastRegister + 1);
            if(0x15 < (int)lastRegister)
            {
                lastRegister = 0;
            }
            this.Log(LogLevel.Noisy, "Auto-incrementing to the next register 0x{0:X}", lastRegister);
        }

        private byte ReadCurrentRegister()
        {
            byte rval = registers.Read((long)lastRegister);
            this.Log(LogLevel.Debug, "ReadCurrentRegister: lastRegister {0} rval 0x{1:X}", lastRegister, rval);
            return rval;
        }

        public void FinishTransmission()
        {
        }

        public void OnGPIO(int number, bool value)
        {
            if (0 != number)
            {
                this.Log(LogLevel.Error, "Unexpected nPDN connection {0}", number);
                return;
            }
            this.Log(LogLevel.Debug, "OnGPIO: number {0} value {1}", number, value);
            // 0==number: active-LOW nPDN signal

            // When LOW the AK4493S enters a low-power state and all
            // internal states are reset. This method will only be
            // called on a HIGH->LOW transition:
            if (false == value)
            {
                this.Log(LogLevel.Info, "Resetting");
                Reset();
            }
        }

        private ByteRegisterCollection CreateRegisters()
        {
            var registersMap = new Dictionary<long, ByteRegister>
            {
                { (long)Registers.Control1, new ByteRegister(this, 0x0C)
		  .WithFlag(0, name: "RSTN")
                  .WithValueField(1, 3, name: "DIF")
		  .WithReservedBits(4, 1)
		  .WithFlag(5, name: "ECS")
		  .WithFlag(6, name: "EXDF")
		  .WithFlag(7, name: "ACKS")
                },
                { (long)Registers.Control2, new ByteRegister(this, 0x22)
		  .WithFlag(0, name: "SMUTE")
                  .WithValueField(1, 2, name: "DEM")
                  .WithValueField(3, 2, name: "DFS")
                  .WithFlag(5, name: "SD")
                  .WithFlag(6, name: "DZFM")
                  .WithFlag(7, name: "DZFE")
                },
                { (long)Registers.Control3, new ByteRegister(this, 0x00)
                  .WithFlag(0, name: "SLOW")
                  .WithFlag(1, name: "SELLR")
                  .WithFlag(2, name: "DZFB")
                  .WithFlag(3, name: "MONO")
                  .WithFlag(4, name: "DCKB")
                  .WithFlag(5, name: "DCKS")
                  .WithFlag(6, FieldMode.Read, name: "ADP")
                  .WithFlag(7, name: "DP")
                },
                { (long)Registers.LchATT, new ByteRegister(this, 0xFF)
                  .WithValueField(0, 8, name: "ATTL")
                },
                { (long)Registers.RchATT, new ByteRegister(this, 0xFF)
                  .WithValueField(0, 8, name: "ATTR")
                },
                { (long)Registers.Control4, new ByteRegister(this, 0x00)
                  .WithFlag(0, name: "SSLOW")
                  .WithFlag(1, name: "DFS2")
		  .WithReservedBits(2, 4)
                  .WithFlag(6, name: "INVR")
                  .WithFlag(7, name: "INVL")
                },
                { (long)Registers.DSD1, new ByteRegister(this, 0x00)
                  .WithFlag(0, name: "DSDSEL0")
                  .WithFlag(1, name: "DSDD")
                  .WithValueField(2, 2, name: "DDMT")
                  .WithFlag(4, name: "DDMODE")
                  .WithFlag(5, FieldMode.Read, name: "DMR")
                  .WithFlag(6, FieldMode.Read, name: "DML")
                  .WithFlag(7, name: "DDM")
                },
                { (long)Registers.Control5, new ByteRegister(this, 0x01)
                  .WithFlag(0, name: "SYNCE")
                  .WithValueField(1, 3, name: "GC")
                  .WithReservedBits(4, 3)
                  .WithFlag(7, name: "MSTBN")
                },
                { (long)Registers.SoundControl, new ByteRegister(this, 0x00)
                  .WithValueField(0, 3, name: "SC")
		  .WithReservedBits(3, 5)
                },
                { (long)Registers.DSD2, new ByteRegister(this, 0x00)
                  .WithFlag(0, name: "DSDSEL1")
                  .WithFlag(1, name: "DSDF")
                  .WithReservedBits(2, 6) // bits 2..4 R/W // bits 5..7 RO
                },
                { (long)Registers.Control6, new ByteRegister(this, 0x04)
                  .WithReservedBits(0, 2)
                  .WithFlag(2, name: "PW")
                  .WithReservedBits(3, 1)
                  .WithValueField(4, 2, name: "SDS")
                  .WithValueField(6, 2, name: "TDM")
                },
                { (long)Registers.Control7, new ByteRegister(this, 0x00)
                  .WithFlag(0, name: "TEST")
                  .WithReservedBits(1, 3)
                  .WithFlag(4, name: "SDS0")
		  .WithReservedBits(5, 1)
                  .WithValueField(6, 2, name: "ATS")
                },
                { (long)Registers.Control8, new ByteRegister(this, 0x00)
                  .WithReservedBits(0, 5)
                  .WithValueField(5, 2, name: "ADPT")
                  .WithFlag(7, name: "ADPE")
                },
            };
            return new ByteRegisterCollection(this, registersMap);
        }

        private readonly ByteRegisterCollection registers;

        private Registers lastRegister;

        private enum Registers : byte
        {
	    Control1     = 0x00,
	    Control2     = 0x01,
	    Control3     = 0x02,
	    LchATT       = 0x03,
	    RchATT       = 0x04,
	    Control4     = 0x05,
	    DSD1         = 0x06,
	    Control5     = 0x07,
	    SoundControl = 0x08,
	    DSD2         = 0x09,
	    Control6     = 0x0A,
	    Control7     = 0x0B,
	    Control8     = 0x15
        }
    }
}
