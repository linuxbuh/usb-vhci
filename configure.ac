AC_PREREQ(2.63)
AC_INIT(Vhci, 1.5)
AC_CONFIG_SRCDIR([Usb/Urb.cs])
AC_CONFIG_MACRO_DIR([m4])

AM_INIT_AUTOMAKE([foreign dist-bzip2 filename-length-max=99 no-dependencies std-options tar-ustar])
AM_MAINTAINER_MODE

MY_CHECK_WS

# pkg-config
AC_PATH_PROG(PKG_CONFIG, pkg-config, no)
if test "x$PKG_CONFIG" = "xno"; then
	AC_MSG_ERROR([You need to install pkg-config])
fi

# tr
AC_PATH_PROG(TR, tr, no)
if test "x$TR" = "xno"; then
	AC_MSG_ERROR([You need to install tr (coreutils)])
fi

AC_PROG_INSTALL

# csc/gmcs/mcs C# compiler
MY_PROG_CSC

# check if we are using mono >= 2.0.1
if test "x$CSC_RUNTIME" = "xmono"; then
	AC_MSG_CHECKING([for mono $(basename $CSC) >= 2.0.1])
	csc_ver="$($CSC --version | sed -e 's/.*@<:@vV@:>@ersion \(@<:@0-9@:>@*\.@<:@0-9@:>@*\(\.@<:@0-9@:>@*\)\?\).*/\1/g')"
	csc_ver_maj=$(echo $csc_ver | cut -d . -f 1)
	csc_ver_min=$(echo $csc_ver | cut -d . -f 2)
	csc_ver_rev=$(echo $csc_ver | cut -d . -f 3)
	if test "$csc_ver_maj" -lt 2 ||
	   test "x$csc_ver_maj" = "x2" -a "x$csc_ver_min" = "x0" -a "$csc_ver_rev" -lt 1; then
		AC_MSG_RESULT([no ($csc_ver)])
		AC_MSG_ERROR([mono < 2.0.1 is not supported])
	else
		AC_MSG_RESULT([yes ($csc_ver)])
	fi
fi


MY_ARG_ENABLE(
	doc,
	auto,
	[Generates the documentation.],
	[yes, no, auto]
)

MY_ARG_ENABLE(
	update-docsrc,
	no,
	[Updates the documentation sources.],
	[yes, no, auto]
)


# mdoc
need_mdoc=""
if test -n "$enable_doc_expl" && test "x$enable_doc" = "xyes"; then
	need_mdoc=yes
fi
if test -n "$enable_update_docsrc_expl" && test "x$enable_update_docsrc" = "xyes"; then
	need_mdoc=yes
fi

MY_PROG_MDOC($need_mdoc)

if test -n "$MDOC"; then
	if test "x$enable_doc" = "xauto"; then
		enable_doc=yes
	fi
	if test "x$enable_update_docsrc" = "xauto"; then
		enable_update_docsrc=yes
	fi
else
	enable_doc=no
	enable_update_docsrc=no
fi

AM_CONDITIONAL(ENABLE_DOC, [test "x$enable_doc" != "xno"])
AM_CONDITIONAL(ENABLE_UPDATE_DOCSRC, [test "x$enable_update_docsrc" != "xno"])

MY_PROG_MONODOC


MY_ARG_ENABLE(
	debug,
	no,
	[Compile with debugging flags.]
)

AM_CONDITIONAL(ENABLE_DEBUG, [test "x$enable_debug" != "xno"])
AM_CONDITIONAL(ENABLE_RELEASE, [test "x$enable_debug" = "xno"])

MY_CSC_CHECK_ARGS([$enable_debug])


MY_ARG_ENABLE(
	examples,
	no,
	[Build the files in the examples subdirectory.]
)

AM_CONDITIONAL(ENABLE_EXAMPLES, [test "x$enable_examples" != "xno"])


MY_ARG_ENABLE(
	gac,
	no,
	[Install assemblies into global assembly cache.]
)

AM_CONDITIONAL(ENABLE_GAC, [test "x$enable_gac" != "xno"])

# gactuil
need_gacutil=""
if test "x$enable_gac" != "xno"; then
	need_gacutil=yes
fi

MY_PROG_GAC_UTIL($need_gacutil)


MY_ARG_WITH(
	snk,
	[${srcdir}/snk/usb-vhci.snk],
	[Sign the assemblies with given key.],
	[path to snk file]
)

if test "x$with_snk" != "xno" && ! test -f "$with_snk"; then
	AC_MSG_ERROR([$with_snk not found])
fi

AC_SUBST(SNK_FILE, $(test "x$with_snk" != "xno" && cd $(dirname $with_snk) && echo $(pwd)/$(basename $with_snk)))
AC_SUBST(SNK_SIGN_BOOL, $(test "x$with_snk" != "xno" && echo true || echo false))

if test "x$enable_gac" != "xno" && test "x$with_snk" = "xno"; then
	AC_MSG_ERROR([assemblies need a strongname to be installed into gac. (do not use --enable-gac and --without-snk at the same time.)])
fi


AC_SUBST(USB_DLL_VERSION,                   $VERSION.2.9)
AC_SUBST(USB_CLASS_MASSSTORAGE_DLL_VERSION, $VERSION.0.1)
AC_SUBST(USB_EMULATION_DLL_VERSION,         $VERSION.0.2)
AC_SUBST(USB_FILTER_DLL_VERSION,            $VERSION.0.0)
AC_SUBST(USB_USBFS_DLL_VERSION,             $VERSION.1.7)
AC_SUBST(VHCI_DLL_VERSION,                  $VERSION.2.5)
AC_SUBST(VHCI_NET_DLL_VERSION,              $VERSION.1.7)
AC_SUBST(VHCICLIENT_VERSION,                $VERSION.1.5)
AC_SUBST(VHCISERVER_VERSION,                $VERSION.1.4)
AC_SUBST(VHCIGUI_VERSION,                   $VERSION.0.2)


AC_CONFIG_FILES([
Usb/AssemblyInfo.cs
Usb/usb.pc
Usb.Class.MassStorage/AssemblyInfo.cs
Usb.Class.MassStorage/usb.class.massstorage.pc
Usb.Emulation/AssemblyInfo.cs
Usb.Emulation/usb.emulation.pc
Usb.Filter/AssemblyInfo.cs
Usb.Filter/usb.filter.pc
Usb.UsbFS/AssemblyInfo.cs
Usb.UsbFS/usb.usbfs.pc
Vhci/AssemblyInfo.cs
Vhci/vhci.pc
Vhci.Net/AssemblyInfo.cs
Vhci.Net/vhci.net.pc
VhciClient/AssemblyInfo.cs
VhciClient/vhciclient
VhciServer/AssemblyInfo.cs
VhciServer/vhciserver
VhciGui/AssemblyInfo.cs
VhciGui/vhcigui
Usb/Makefile
Usb.Class.MassStorage/Makefile
Usb.Emulation/Makefile
Usb.Filter/Makefile
Usb.UsbFS/Makefile
Vhci/Makefile
Vhci.Net/Makefile
VhciClient/Makefile
VhciServer/Makefile
VhciGui/Makefile
examples/Forwarding/Makefile
examples/HelloUsb/Makefile
examples/HelloUsb2/Makefile
examples/InvertMouse/Makefile
examples/InvertMouse2/Makefile
examples/ScanBus/Makefile
examples/Makefile
Makefile
])

AC_OUTPUT

echo
echo
echo "=== CONFIGURATION SUMMARY ==="
echo
echo "C# compiler:                        $CSC"
echo "Mdoc:                               $MDOC"
echo "Monodoc sources directory:          $MONODOC_SOURCES_DIR"
echo "Gacutil:                            $GAC_UTIL"
echo "C# compiler arguments:              $csc_args_eval"
echo "Debugging enabled:                  $enable_debug"
echo "Generate documentation:             $enable_doc"
echo "Update documentation sources:       $enable_update_docsrc"
echo "Build examples:                     $enable_examples"
echo "Sign assemblies with snk:           $with_snk"
echo "Install into GAC:                   $enable_gac"
