
EXTRA_DIST = 

COMPILE_TARGET = library
ASSEMBLY_COMPILER = CSC
CSC_ARGS = $(CSC_UNSAFE) $(CSC_STRONGNAME)

all: $(ASSEMBLY) $(LINUX_PKGCONFIG) $(ASSEMBLY_WRAPPER) $(DOC_TARGETS)

FILES =  \
	AssemblyInfo.cs \
	CancelUrbWork.cs \
	Forwarders/IForwarder.cs \
	Forwarders/SingleDeviceForwarder.cs \
	Hcd.cs \
	Linux/Ioctl.cs \
	Linux/LocalHcd.cs \
	LocalHcd.cs \
	PortStat.cs \
	PortStatChangeFlags.cs \
	PortStatFlags.cs \
	PortStatStatusFlags.cs \
	PortStatWork.cs \
	PortStatWorkTriggerFlags.cs \
	ProcessUrbWork.cs \
	Windows/Ioctl.cs \
	Windows/LocalHcd.cs \
	Work.cs 

DATA_FILES = 

RESOURCES = 

EXTRAS = 

REFERENCES = System

PROJECT_REFERENCES = ../bin/Usb.dll

DLL_REFERENCES = 

CLEANFILES = 

include $(top_srcdir)/Makefile.include
