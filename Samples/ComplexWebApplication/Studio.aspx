﻿<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Studio.aspx.cs" Inherits="ComplexWebApplication.Studio" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
    <link type="text/css" href="css/reset.css" rel="Stylesheet" />
    <link type="text/css" href="css/ui-darkness/jquery-ui-1.8.16.custom.css" rel="Stylesheet" />	
	<link rel="stylesheet" href="css/jquery.Jcrop.css" type="text/css" /> 
 
</head>
<body>
    <form id="form1" runat="server">
    <div id="studio">
    <table><tr><td style="vertical-align:top">
    <div class="controls ui-helper-reset">
     </div>
     </td><td style="text-align:center; vertical-align:middle">
     <img class="img" runat="server" src="~/private/redeye/Red-Eye_08.jpg?width=400" style="margin:auto;" />
     </td></tr></table>
<script type="text/javascript" src="js/jquery-1.6.2.min.js"></script>
<script type="text/javascript" src="js/jquery-ui-1.8.16.custom.min.js"></script>
<script type="text/javascript" src="js/querystring.js"></script>
<script src="js/jquery.Jcrop.js" type="text/javascript"></script> 
<script src="js/jquery.jcrop.preview.js" type="text/javascript"></script> 
<script type="text/javascript" src="js/studio.js"></script>

    </div>
    </form>
</body>
</html>
