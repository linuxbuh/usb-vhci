
EXTRA_DIST = 

COMPILE_TARGET = library
ASSEMBLY_COMPILER = CSC
CSC_ARGS = $(CSC_UNSAFE) $(CSC_STRONGNAME)

all: $(ASSEMBLY) $(LINUX_PKGCONFIG) $(ASSEMBLY_WRAPPER) $(DOC_TARGETS)

FILES =  \
	AssemblyInfo.cs \
	DeviceFile.cs \
	Enumeration/Device.cs \
	Enumeration/Enumerator.cs \
	Enumeration/Hub.cs \
	Enumeration/Hub_Interfaces.cs \
	Enumeration/RootHub.cs \
	Ioctl.cs \
	UsbDevice.cs 

DATA_FILES = 

RESOURCES = 

EXTRAS = 

REFERENCES = System

PROJECT_REFERENCES = ../bin/Usb.dll

DLL_REFERENCES = 

CLEANFILES = 

include $(top_srcdir)/Makefile.include
