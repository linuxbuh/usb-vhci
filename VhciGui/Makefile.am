
EXTRA_DIST = 

COMPILE_TARGET = windows
ASSEMBLY_COMPILER = CSC
CSC_ARGS = $(CSC_NO_UNSAFE) $(CSC_STRONGNAME)

all: $(ASSEMBLY) $(LINUX_PKGCONFIG) $(ASSEMBLY_WRAPPER) $(DOC_TARGETS)

FILES =  \
	AssemblyInfo.cs \
	DescriptorNode.cs \
	DeviceNode.cs \
	MainForm.cs \
	MainForm.Designer.cs \
	Node.cs \
	Program.cs 

DATA_FILES = 

RESOURCES = 

EXTRAS = 

REFERENCES =  \
	System \
	System.Data \
	System.Drawing \
	System.Windows.Forms

PROJECT_REFERENCES =  \
	../bin/Usb.dll \
	../bin/Usb.UsbFS.dll \
	../bin/Vhci.dll \
	../bin/Vhci.Net.dll

DLL_REFERENCES = 

CLEANFILES = 

include $(top_srcdir)/Makefile.include
