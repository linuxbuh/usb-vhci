
EXTRA_DIST = 

COMPILE_TARGET = console
ASSEMBLY_COMPILER = CSC
CSC_ARGS = $(CSC_NO_UNSAFE) $(CSC_STRONGNAME)

all: $(ASSEMBLY) $(LINUX_PKGCONFIG) $(ASSEMBLY_WRAPPER) $(DOC_TARGETS)

FILES =  \
	AssemblyInfo.cs \
	Main.cs 

DATA_FILES = 

RESOURCES = 

EXTRAS = 

REFERENCES = System

PROJECT_REFERENCES =  \
	../bin/Usb.dll \
	../bin/Vhci.dll \
	../bin/Vhci.Net.dll

DLL_REFERENCES = 

CLEANFILES = 

include $(top_srcdir)/Makefile.include
