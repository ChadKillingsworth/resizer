﻿/* Copyright (c) 2011 Wouter A. Alberts and Nathanael D. Jones. See license.txt for your rights. */
using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;

namespace AzureWebImages {
    public partial class Upload : System.Web.UI.Page {

        static CloudBlobClient cloudBlobClient;

        protected void Page_Load(object sender, EventArgs e) {
            // Initialize container settings
            if (!IsPostBack)
                SetContainerAndPermissions();
        }

        protected void btnSubmit_Click(object sender, EventArgs e) {
            if (Page.IsValid) {
                if (fuPicture.HasFile == true && fuPicture.FileBytes.Length > 0) {

                    string[] extensions = { ".jpg", ".jpeg", ".gif", ".bmp", ".png" };
                    bool isImage = extensions.Any(x => x.Equals(Path.GetExtension(fuPicture.FileName.ToLower()), StringComparison.OrdinalIgnoreCase));

                    if (isImage) {
                        // Store the uploaded file as Blob in the Cloud storage

                        // Get the reference of the container in which the blobs are stored
                        CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference("imageresizer");

                        // Set the name of the uploaded document to a unique name
                        string filename = Guid.NewGuid().ToString() + ".jpg";

                        // Get the blob reference and set its metadata properties
                        CloudBlockBlob blob = cloudBlobContainer.GetBlockBlobReference(filename);
                        blob.Properties.ContentType = fuPicture.PostedFile.ContentType;
                        blob.UploadFromStream(fuPicture.FileContent);

                        // Display images; use relative paths so the module will capture the urls
                        StringBuilder sb = new StringBuilder(2000);
                        sb.Append("<img src=\"azure/imageresizer/" + filename + "?width=75\" border=\"0\"><br /><br />");
                        sb.Append("<img src=\"/azure/imageresizer/" + filename + "?width=150&height=150&crop=auto\" border=\"0\"><br /><br />");
                        sb.Append("<img src=\"/azure/imageresizer/" + filename + "\" border=\"0\">");

                        litImages.Text = sb.ToString();
                    }
                }
            }
        }

        private void SetContainerAndPermissions() {
            try {
                // Creating the container
                var cloudStorageAccount = CloudStorageAccount.FromConfigurationSetting("BlobConn");

                cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();
                CloudBlobContainer blobContainer = cloudBlobClient.GetContainerReference("imageresizer");
                blobContainer.CreateIfNotExist();

                var containerPermissions = blobContainer.GetPermissions();
                containerPermissions.PublicAccess = BlobContainerPublicAccessType.Container;
                blobContainer.SetPermissions(containerPermissions);
            }
            catch (Exception Ex) {
                throw new Exception("Error while creating the container: " + Ex.Message);
            }
        }
    }
}