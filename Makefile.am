EXTRA_DIST = \
m4 \
AUTHORS \
COPYING \
INSTALL \
README \
snk/usb-vhci.snk

ACLOCAL_AMFLAGS = -I m4

SUBDIRS = \
Usb \
Usb.Class.MassStorage \
Usb.Emulation \
Usb.Filter \
Usb.UsbFS \
Vhci \
Vhci.Net \
VhciServer \
VhciClient \
VhciGui

if ENABLE_EXAMPLES
SUBDIRS += examples
endif

DISTCLEANFILES = \
$(builddir)/test_out.exe* \
$(builddir)/test_in.* \
$(builddir)/*.userprefs \
$(builddir)/*.usertasks

distclean-local:
	rm -r -f '$(builddir)/bin' '$(builddir)/obj' '$(builddir)/autom4te.cache'

uninstall-local: rm-html-doc-dir
	-rmdir '$(DESTDIR)@libdir@/@PACKAGE@'

if ENABLE_DOC
rm-html-doc-dir:
	-rmdir '$(DESTDIR)$(htmldir)'
else
rm-html-doc-dir:
endif

MAINTAINERCLEANFILES = \
configure \
Makefile.in \
aclocal.m4

DISTCHECK_CONFIGURE_FLAGS = MONODOC_SOURCES_DIR="$$dc_install_base/lib/monodoc/sources" --enable-doc --disable-update-docsrc --enable-examples

